require 'socket'
require 'pathname'

class VFF
  def initialize()
    @foundvff = false
    @findtext = ""
    @greptext = ""

    pn = Pathname.pwd
    while (!pn.root?)
      tpn = pn + ".vff"
      if (tpn.exist?)
        @foundvff = true
        @vffpath = pn + ".vff"
        @path = pn
        return true
      end
      pn = pn + ".."
      pn = Pathname.new(pn.cleanpath())
    end
  end

  def enter(mode)
    buffer = VIM::Buffer.current
    if (@foundvff)

      connect()

      while (buffer.count > 1)
        VIM::command(":  echo '" + buffer.count + "'")
        buffer.delete(1)
      end

      buffer.append(0, "VimFastFind: Ctrl-F for file mode, Ctrl-E for grep mode");
      buffer.append(1, "<ESC> to quit, <UP>/<DOWN> or Alt-J/Alt-K to move, <ENTER> to select");
      buffer.append(2, "----------------------------------------------------------------------");

      buffer.append(3, "Root: " + @path.to_s())
      buffer.append(4, "")
      if (mode == 'find')
        buffer.append(5, "Find File: ")
      else
        buffer.append(5, "Find Content: ")
      end
      buffer.append(6, "")
      buffer.append(7, "")
      while (buffer.count >= 8)
        buffer.delete(8)
      end
      VIM::command(":  aug ListFiles")
      VIM::command(":    exec \"au WinEnter \" . g:vffWindowName . \" call VffSetupSelect ()\"")
      VIM::command(":    exec \"au WinLeave \" . g:vffWindowName . \" call VffUnsetupSelect ()\"")
      VIM::command(":    exec \"au BufLeave \" . g:vffWindowName . \" call VffClearSetup ()\"")
      VIM::command(":  aug END")
      VIM::command(":  setlocal cursorline")
      VIM::command(":  normal G")
      VIM::command(":  call VffSetupSelect ()")

      _refresh(mode, true)
    else
      VIM::command(":  call VffSetupBadSelect ()")
      i = 0
      for l in usage.split("\n")
        buffer.append(i, l)
        i += 1
      end
    end
  end

  def change_config(vffpath)
    @vffpath = vffpath
    connect()
    @sock.puts('config ' + vffpath)
  end

  def connect()
    if (!@foundvff)
      return false
    end

    begin
      if (@sock)
        @sock.puts('nop')
        @sock.gets
      end
    rescue
      @sock = nil
    end

    if (!@sock)
      i = 0
      begin
        connect2()
      rescue
        job = fork do
          if (RUBY_PLATFORM == 'i386-cygwin' or RUBY_PLATFORM == 'x86_64-cygwin')
            exec __dir__ + "/VFFServer.exe"
          else
            exec "mono " + __dir__ + "/VFFServer.exe"
          end
        end
        Process.detach(job)

        i = 0
        while (i < 50)
          sleep(0.0100)
          begin
            connect2()
            i = 99
          rescue
          end
        end
      end
    end
  end

  def connect2()
    @sock = TCPSocket.open("127.0.0.1", 20398)
    @sock.puts('config ' + @vffpath.to_s())
  end

  def text_append(mode, s)
    if (mode == 'find')
      @findtext += s
    else
      @greptext += s
    end
    # don't update results until refresh is called
    _refresh(mode, false)
  end

  def text_backspace(mode)
    if (mode == 'find')
      @findtext.chop!()
    else
      @greptext.chop!()
    end
    # don't update results until refresh is called
    _refresh(mode, false)
  end

  def text_clear(mode)
    if (mode == 'find')
      @findtext = ''
    else
      @greptext = ''
    end
    # update results immediately
    _refresh(mode, true)
  end

  def text_set(mode, s)
    if (mode == 'find')
      @findtext = s
    else
      @greptext = s
    end
  end

  def refresh(mode)
    _refresh(mode, true)
  end

  def _refresh(mode, domatching)
    _refresh2(mode, domatching, true)
  end

  def _refresh2(mode, domatching, doretry)
    if (!@foundvff)
      return false
    end
    buffer = VIM::Buffer.current
    buffer.delete(6)
    if (mode == 'find')
      buffer.append(5, "Find File: " + @findtext)
      text = @findtext
    else
      buffer.append(5, "Find Content: " + @greptext)
      text = @greptext
    end
    while (buffer.count >= 7)
      buffer.delete(7)
    end
    if domatching
      connect()
      begin
        if ((mode == "find" && text != "") || (mode == "grep" && text.length >= 3))
          if (mode == 'find')
            @sock.puts("find match " + text)
          else
            @sock.puts("grep match " + text)
          end
          while line = @sock.gets
            line = line.gsub(/\r\n?/, "\n").chop
            if (line == "")
              break
            end
            buffer.append(buffer.count, line)
          end
          if (line == nil && doretry)
            connect()
            _refresh2(mode, true, false)
          end
        end
      rescue
        if (doretry)
          connect()
          _refresh2(mode, true, false)
        end
      end
    end

    buffer.append(buffer.count, "")
    VIM::command("set nomodified")
  end

  def relativepath(relativeto,abspath)
    abspath = @path.to_s() + abspath
    path = abspath.split("/")
    rel = relativeto.split("/")
    while (path.length > 0) && (path.first == rel.first)
      path.shift
      rel.shift
    end
    VIM::command("let g:vffrubyret = \"" + (('..' + "/") * (rel.length) + path.join("/")) + "\"")
  end

    def usage
      return <<EOS
ERROR: No .vff file found!

Hit ESCAPE or ENTER to close this window

In the root of the filesystem tree you want to scan, create a .vff file.

After that, you can include or exclude files using the following statements:

[file|grep] include <pattern>
[file|grep] exclude <pattern>

You can include/exclude for just find mode or just grep mode by prefixing the
include/exclude statement with "file" or "grep". Not specifying "file" or
"grep" will cause the include/exclude to match for both.

Patterns are matched in order and short circuit on match. Unmatched files will
be excluded.

"#" is the start of a comment on any line. Blank lines are ignored.


Example:

% cat .vff
include *.c
include *.cs
include *.cpp
include *.h
include *.java
include *.lua
include *.pl
include *.py
include *.rb
include *.tcl
include *.awk
include *.sed
include *.sh
include *.bash
EOS
    end
end
