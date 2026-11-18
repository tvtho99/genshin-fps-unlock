// Self-test for Utils. Build and run: mingw32-make -C UnlockerStub test
#undef NDEBUG
#include <cassert>
#include <cstdio>
#include "../Utils.h"

namespace
{
	constexpr uint64_t Rel32Reach = 0x7FFFFF00;
	constexpr uint64_t SysMin = 0x10000;
	constexpr uint64_t SysMax = 0x7FFF'FFFE'FFFF;

	void TestAllocRangeStaysWithinRel32()
	{
		constexpr uint64_t target = 0x7FF6'0000'0000;
		const auto [lo, hi] = Utils::ComputeAllocRange(target, SysMin, SysMax);
		assert(lo == target - Rel32Reach);
		assert(hi == target + Rel32Reach);
	}

	void TestAllocRangeLowTargetDoesNotUnderflow()
	{
		constexpr uint64_t target = 0x40000000;
		const auto [lo, hi] = Utils::ComputeAllocRange(target, SysMin, SysMax);
		assert(lo == SysMin);
		assert(hi == target + Rel32Reach);
	}

	void TestAllocatePageIsReachableFromTarget()
	{
		const auto target = reinterpret_cast<uint8_t*>(GetModuleHandleW(nullptr));
		const auto page = Utils::AllocatePage(target, 0x1000, PAGE_READWRITE);
		assert(page != nullptr);
		const auto distance = page > target ? page - target : target - page;
		assert(static_cast<uint64_t>(distance) < Rel32Reach);
		VirtualFree(page, 0, MEM_RELEASE);
	}

	void TestPatternScanNeverReadsPastSpan()
	{
		constexpr size_t pageSize = 0x1000;
		const auto base = static_cast<uint8_t*>(VirtualAlloc(nullptr, pageSize * 2, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE));
		assert(base != nullptr);
		DWORD oldProtect = 0;
		assert(VirtualProtect(base + pageSize, pageSize, PAGE_NOACCESS, &oldProtect));

		base[0x100] = 0xAA;
		base[0x101] = 0xBB;
		// Same bytes at the very end of the readable page: a 16-byte load here would hit PAGE_NOACCESS.
		base[pageSize - 3] = 0xAA;
		base[pageSize - 2] = 0xBB;
		base[pageSize - 1] = 0xCC;

		const auto results = Utils::PatternScanAll({ base, pageSize }, "AA BB");
		assert(results.size() == 1);
		assert(results.front() == base + 0x100);

		VirtualFree(base, 0, MEM_RELEASE);
	}

	void TestGetModulePathReturnsThisExe()
	{
		const auto path = Utils::GetModulePath(nullptr);
		assert(path.ends_with(L"utils_selftest.exe"));
	}
}

int main()
{
	TestAllocRangeStaysWithinRel32();
	TestAllocRangeLowTargetDoesNotUnderflow();
	TestAllocatePageIsReachableFromTarget();
	TestPatternScanNeverReadsPastSpan();
	TestGetModulePathReturnsThisExe();
	std::puts("utils_selftest: all passed");
	return 0;
}
