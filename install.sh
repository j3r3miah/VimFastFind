#!/bin/sh

./build.sh release
mkdir -p ~/.vim/plugin/VFF
cp VFF.vim ~/.vim/plugin/
cp VFF.rb ~/.vim/plugin/VFF
cp bin/release/*.exe ~/.vim/plugin/VFF
cp bin/release/*.dll ~/.vim/plugin/VFF
hash=`git rev-parse --short HEAD`
if [ ! -z "$hash" ]; then
    echo $hash > ~/.vim/plugin/VFF/version
fi
