#!/bin/sh

if [ "$1" == "release" ]; then
    BUILD_DIR="build/release"
    FLAGS="-debug:pdbonly /optimize -d:NDEBUG"
else
    BUILD_DIR="build/debug"
    FLAGS="/debug -d:TRACE -d:DEBUG"
fi

mkdir -p $BUILD_DIR/

cp -p ./MonoMac.dll $BUILD_DIR/

mcs -platform:x64 -noconfig -d:MONO /nologo /out:$BUILD_DIR/VFFServer.exe \
    /target:exe /unsafe /warn:4 \
    $FLAGS \
    -d:PLATFORM_OSX -d:HAVE_MONO_POSIX -d:SYSTEM_OSX -d:ARCH_X64 -d:SYSTEM_MACOSX -d:PLATFORM_MACOSX \
    /r:System.dll /r:System.Core.dll /r:Mono.Posix.dll /r:./MonoMac.dll \
    ./server.cs \
    ./matcher.cs \
    ./config.cs \
    ./utils.cs \
    ./DirectoryWatcher.cs \
    ./VolumeWatcher.cs \
    ./osx_utils.cs \
    ./logger.cs

cat > ./$BUILD_DIR/VFFServer <<- 'EOM'
#!/bin/sh
MY_PATH="`dirname \"$0\"`"
MY_PATH="`( cd \"$MY_PATH\" && pwd )`"
export DYLD_LIBRARY_PATH="$MY_PATH:$DYLD_LIBRARY_PATH"
if [ x$GDB = x1 ]; then export MAYBE_DEBUG="gdb --args"; fi
if [ x$LLDB = x1 ]; then export MAYBE_DEBUG="lldb -o run"; export MAYBE_DEBUG2="--"; fi
exec $MAYBE_DEBUG mono64 $MAYBE_DEBUG2 --debug "$MY_PATH/VFFServer.exe" "$@"
EOM

chmod +x ./$BUILD_DIR/VFFServer
