CXX = x86_64-w64-mingw32-g++
CC = x86_64-w64-mingw32-gcc
CPPFLAGS = -DNDEBUG -DNOMINMAX -DZYDIS_STATIC_BUILD -DZYCORE_STATIC_BUILD
CXXFLAGS = -std=c++23 -O2
CFLAGS = -O2
OBJS = dllmain.o Utils.o Zydis.o
DLL = UnlockerStub.dll
SELFTEST = utils_selftest.exe

all: $(DLL)

$(DLL): $(OBJS)
	$(CXX) -shared -static -static-libgcc -static-libstdc++ -o $@ $^ -Wl,--out-implib,lib$(basename $@).a -lntdll

%.o: %.cpp Utils.h
	$(CXX) $(CPPFLAGS) $(CXXFLAGS) -c $< -o $@

Zydis.o: Zydis.c Zydis.h
	$(CC) $(CPPFLAGS) $(CFLAGS) -c $< -o $@

# Self-test for Utils (asserts must stay active, so no -DNDEBUG here)
$(SELFTEST): tests/utils_selftest.cpp Utils.cpp Utils.h
	$(CXX) -DNOMINMAX $(CXXFLAGS) -static -o $@ tests/utils_selftest.cpp Utils.cpp

test: $(SELFTEST)
	./$(SELFTEST)

clean:
	rm -f $(OBJS) $(DLL) lib$(basename $(DLL)).a $(SELFTEST)

.PHONY: all test clean
