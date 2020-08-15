#!/bin/sh

mkdir -p build/

cp -p ./MonoMac.dll build/

# debug build
# mcs -platform:x64 -noconfig -d:MONO /nologo /out:build/VFFServer.exe -d:SYSTEM_OSX /unsafe -d:SYSTEM_MACOSX /warn:4 -d:ARCH_X64 /debug /target:exe -d:DEBUG -d:HAVE_MONO_POSIX -d:TRACE -d:PLATFORM_MACOSX -d:PLATFORM_OSX /r:System.dll /r:System.Core.dll /r:Mono.Posix.dll /r:./MonoMac.dll ./server.cs ./utils.cs ./DirectoryWatcher.cs ./VolumeWatcher.cs ./osx_utils.cs

# release build
mcs -platform:x64 -noconfig -d:MONO /nologo /out:build/VFFServer.exe /target:exe -debug:pdbonly /warn:4 /optimize /unsafe \
    -d:PLATFORM_OSX -d:HAVE_MONO_POSIX -d:SYSTEM_OSX -d:ARCH_X64 -d:NDEBUG -d:SYSTEM_MACOSX -d:PLATFORM_MACOSX \
    /r:System.dll /r:System.Core.dll /r:Mono.Posix.dll /r:./MonoMac.dll \
    ./server.cs \
    ./matcher.cs \
    ./config.cs \
    ./utils.cs \
    ./DirectoryWatcher.cs \
    ./VolumeWatcher.cs \
    ./osx_utils.cs \
    ./logger.cs

cat > ./build/VFFServer <<- 'EOM'
#!/bin/sh
MY_PATH="`dirname \"$0\"`"
MY_PATH="`( cd \"$MY_PATH\" && pwd )`"
export DYLD_LIBRARY_PATH="$MY_PATH:$DYLD_LIBRARY_PATH"
if [ x$GDB = x1 ]; then export MAYBE_DEBUG="gdb --args"; fi
if [ x$LLDB = x1 ]; then export MAYBE_DEBUG="lldb -o run"; export MAYBE_DEBUG2="--"; fi
exec $MAYBE_DEBUG mono64 $MAYBE_DEBUG2 --debug "$MY_PATH/VFFServer.exe" "$@"
EOM

chmod +x ./build/VFFServer
