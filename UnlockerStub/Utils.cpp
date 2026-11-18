#include "Utils.h"
#include <intrin.h>
#include <array>
#include <ranges>
#include <cmath>
#include <format>

namespace
{
	std::tuple<std::vector<uint8_t>, std::string> PatternToBytes(const char* pattern)
	{
		std::vector<uint8_t> bytes;
		std::string mask;

		const char* current = pattern;
		while (*current)
		{
			if (*current == ' ') {
				++current;
				continue;
			}

			if (*current == '?') {
				bytes.push_back(0);
				mask.push_back('?');

				++current;
				if (*current == '?')
					++current;
				continue;
			}

			char* endPtr = nullptr;
			unsigned long converted = std::strtoul(current, &endPtr, 16);

			if (endPtr != current) {
				bytes.push_back(static_cast<uint8_t>(converted));
				mask.push_back('x');
				current = endPtr;
			}
			else {
				++current;
			}
		}
		return { bytes, mask };
	}
}

std::wstring Utils::GetModulePath(HMODULE hModule)
{
	std::wstring path(MAX_PATH, L'\0');
	DWORD nChars = 0;

	// A full buffer means the path was truncated; GetLastError() is not reliable for this.
	while ((nChars = GetModuleFileNameW(hModule, path.data(), static_cast<DWORD>(path.size()))) == path.size())
		path.resize(path.size() * 2);

	path.resize(nChars);
	return path;

}

std::vector<uint8_t*> Utils::PatternScanAll(std::span<uint8_t> bytes, const char* pattern)
{
	std::vector<uint8_t*> results;

	auto [patternBytes, patternMask] = PatternToBytes(pattern);

	if (patternBytes.empty()) return results;
	if (bytes.size() < patternBytes.size()) return results;

	patternBytes.resize(patternBytes.size() + 16, 0);

	constexpr std::size_t chunkSize = 16;
	const auto maskCount = static_cast<std::size_t>(std::ceil(patternMask.size() / static_cast<float>(chunkSize)));
	std::array<int32_t, 32> masks{}; // Supports patterns up to 512 bytes

	auto chunks = patternMask | std::views::chunk(chunkSize);
	std::size_t maskIdx = 0;
	for (auto chunk : chunks) {
		int32_t mask = 0;
		int bit = 0;
		for (char c : chunk) {
			if (c == 'x') mask |= (1 << bit);
			bit++;
		}
		masks[maskIdx++] = mask;
	}

	// Every candidate position loads maskCount full 16-byte chunks, so stop where the last load still fits.
	// ponytail: matches in the last <16 bytes of the span are skipped; section tails are padding in practice.
	const std::size_t scanLen = maskCount * chunkSize;
	if (bytes.size() < scanLen) return results;

	__m128i patternFirst16 = _mm_loadu_si128(reinterpret_cast<const __m128i*>(patternBytes.data()));

	auto pData = bytes.data();
	const auto end = pData + bytes.size() - scanLen;

	while (pData <= end)
	{
		_mm_prefetch(reinterpret_cast<const char*>(pData + 64), _MM_HINT_NTA);

		if (patternMask[0] == 'x' && patternBytes[0] != pData[0]) {
			++pData;
			continue;
		}

		__m128i dataChunk = _mm_loadu_si128(reinterpret_cast<const __m128i*>(pData));
		__m128i cmp = _mm_cmpeq_epi8(patternFirst16, dataChunk);
		int maskResult = _mm_movemask_epi8(cmp);

		if ((maskResult & masks[0]) == masks[0])
		{
			bool found = true;

			for (std::size_t i = 1; i < maskCount; ++i)
			{
				dataChunk = _mm_loadu_si128(reinterpret_cast<const __m128i*>(pData + i * chunkSize));
				__m128i patChunk = _mm_loadu_si128(reinterpret_cast<const __m128i*>(patternBytes.data() + i * chunkSize));

				cmp = _mm_cmpeq_epi8(dataChunk, patChunk);
				maskResult = _mm_movemask_epi8(cmp);

				if ((maskResult & masks[i]) != masks[i])
				{
					found = false;
					break;
				}
			}

			if (found) {
				results.push_back(pData);
			}
		}

		++pData;
	}

	return results;
}

std::wstring Utils::GetErrorMessage(DWORD errorCode)
{
	
	LPWSTR messageBuffer = nullptr;
	const auto size = FormatMessageW(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
		nullptr, errorCode, GetUserDefaultLangID(), reinterpret_cast<LPWSTR>(&messageBuffer), 0, nullptr);

	std::wstring message(messageBuffer, size);
	LocalFree(messageBuffer);

	return message;

}

void Utils::ShowWin32Error(const std::wstring& apiName)
{

	const auto errorCode = GetLastError();
	const auto errorMessage = GetErrorMessage(errorCode);
	
	std::wstring message = std::format(L"{} failed\nError code: {}\nError message: {}", apiName, errorCode, errorMessage);
	ShowError(message);

}

void Utils::ShowError(const std::wstring& message)
{
	MessageBoxW(nullptr, message.c_str(), L"[FPS Unlocker] Error", MB_ICONERROR);
}

std::pair<uint64_t, uint64_t> Utils::ComputeAllocRange(uint64_t target, uint64_t sysMin, uint64_t sysMax)
{
	constexpr uint64_t rel32Reach = 0x7FFFFF00;
	const uint64_t lower = target > rel32Reach ? target - rel32Reach : 0;
	const uint64_t upper = target + rel32Reach;
	return { std::max(lower, sysMin), std::min(upper, sysMax) };
}

uint8_t* Utils::AllocatePage(uint8_t* desiredAddress, size_t size, DWORD protect)
{
	SYSTEM_INFO sysInfo{};
	GetSystemInfo(&sysInfo);

	// VirtualAlloc rounds reservations down to the allocation granularity (64KB), so step by it.
	const uint64_t granularity = sysInfo.dwAllocationGranularity;
	const auto target = reinterpret_cast<uint64_t>(desiredAddress);
	const auto [lower, upper] = ComputeAllocRange(target,
		reinterpret_cast<uint64_t>(sysInfo.lpMinimumApplicationAddress),
		reinterpret_cast<uint64_t>(sysInfo.lpMaximumApplicationAddress));
	const uint64_t start = target & ~(granularity - 1);

	for (uint64_t offset = granularity; ; offset += granularity)
	{
		const uint64_t highAddr = start + offset;
		const bool highInRange = highAddr + size <= upper;
		const bool lowInRange = start >= offset && start - offset >= lower;

		if (!highInRange && !lowInRange)
			break;

		if (highInRange)
		{
			if (void* outAddr = VirtualAlloc(reinterpret_cast<void*>(highAddr), size, MEM_COMMIT | MEM_RESERVE, protect))
				return static_cast<uint8_t*>(outAddr);
		}

		if (lowInRange)
		{
			if (void* outAddr = VirtualAlloc(reinterpret_cast<void*>(start - offset), size, MEM_COMMIT | MEM_RESERVE, protect))
				return static_cast<uint8_t*>(outAddr);
		}
	}

	return nullptr;
}
