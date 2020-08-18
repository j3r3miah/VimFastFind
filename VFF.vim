" The key sequence that should activate the buffer browser. The default is ^F.
"   Enter the key sequence in a single quoted string, exactly as you would use
"   it in a map command.
if !exists("g:vffFindActKeySeq")
  let vffFindActKeySeq = '<C-F>'
endif

if !exists("g:vffGrepActKeySeq")
  let vffGrepActKeySeq = '<C-E>'
endif

if !exists("g:vffSearchActKeySeq")
  let vffSearchActKeySeq = '<C-S>'
endif

if !exists("g:vffChooseConfigKeySeq")
  let vffChooseConfigKeySeq = '<C-Q>'
endif

" The name of the browser. The default is "/---Select File---", but you can
"   change the name at your will. A leading '/' is advised if you change
"   directories from with in vim.
let vffWindowName = '/---\ Select\ File\ ---'

" A non-zero value for the variable vffRemoveBrowserBuffer means that after
"   the selection is made, the buffer that belongs to the browser should be
"   deleted. But this is not advisable as vim doesn't reuse the buffer numbers
"   that are no longer used. The default value is 0, i.e., reuse a single
"   buffer. This will avoid creating lots of gaps and quickly reach a large
"   buffer numbers for the new buffers created.
let vffRemoveBrowserBuffer = 1

" A non-zero value for the variable highlightOnlyFilename will highlight only
"   the filename instead of the whole path. The default value is 0.
let highlightOnlyFilename = 0

" Your can configure a delay in between when typing stops and results list.
"   To enable the delay, add to your .vimrc:
"      let g:vff_debounce = 1
"   The default is 100 ms. To change it to 50 ms, add to your .vimrc:
"      let g:vff_debounce_delay = 50
"
if exists("g:vff_debounce")
  if exists("g:vff_debounce_delay")
    let g:vff_refreshdelay = g:vff_debounce_delay
  else
    let g:vff_refreshdelay = 100
  endif
endif

"
" END configuration.
"

" The header and text entry take up 6 lines, so the first result is line 7
let firstResultLineNumber = 7

function! VffSetupActivationKey ()
  exec 'nnoremap ' . g:vffFindActKeySeq . ' :call VffListBufs ("find")<CR>'
  exec 'vnoremap ' . g:vffFindActKeySeq . ' :call VffListBufs ("find")<CR>'
  exec 'nnoremap ' . g:vffGrepActKeySeq . ' :call VffListBufs ("grep")<CR>'
  exec 'vnoremap ' . g:vffGrepActKeySeq . ' :call VffListBufs ("grep")<CR>'
  exec 'nnoremap ' . g:vffSearchActKeySeq . ' :call VffSearch ("normal")<CR>'
  exec 'vnoremap ' . g:vffSearchActKeySeq . ' :call VffSearch ("visual")<CR>'
  exec 'nnoremap ' . g:vffChooseConfigKeySeq . ' :call VffChooseConfig()<CR>'
endfunction

function! VffSetupDeActivationKey ()
  exec 'nnoremap ' . g:vffFindActKeySeq . ' :call VffDeActivate ("find")<CR>'
  exec 'vnoremap ' . g:vffFindActKeySeq . ' :call VffDeActivate ("find")<CR>'
  exec 'nnoremap ' . g:vffGrepActKeySeq . ' :call VffDeActivate ("grep")<CR>'
  exec 'vnoremap ' . g:vffGrepActKeySeq . ' :call VffDeActivate ("grep")<CR>'
  exec 'nnoremap ' . g:vffSearchActKeySeq . ' :call VffDeActivate ("grep")<CR>'
  exec 'vnoremap ' . g:vffSearchActKeySeq . ' :call VffDeActivate ("grep")<CR>'
endfunction

call VffSetupActivationKey ()

let g:vff_greplastline = -1
let g:vff_findlastline = -1

function! VffListBufs (mode)
  let g:vff_mode = a:mode
  let g:vff_savetimeoutlen = &timeoutlen
  let g:vff_origwin = winnr()
  let l:saveReport = &report
  let &timeoutlen=0
  let &report=10000
  split
  setlocal noswapfile
  silent! exec ":e " . g:vffWindowName
  setlocal noswapfile
  let g:vff_vffwin = winnr()
  if g:vff_mode == 'find'
    syn match Title "Find File:.*"
  else
    syn match Title "Find Content:.*"
  endif
  syn match Title "----------------*"
  hi CursorLine   cterm=NONE ctermbg=darkblue ctermfg=white
  setlocal cc=
  let &report = l:saveReport
  exec 'ruby $vff.enter("' . g:vff_mode . '")'
  if g:vff_mode == 'grep' && g:vff_greplastline != -1
    exec g:vff_greplastline
  elseif g:vff_mode == 'find' && g:vff_findlastline != -1
    exec g:vff_findlastline
  endif
  set nomodified
  call VffGoToFirstResult ()
endfunction

function! VffSearch (vimMode)
  if a:vimMode == 'visual'
    let s:query = VffGetSelection ()
  else
    let s:query = expand ('<cword>')
  endif
  exec "ruby $vff.text_set('grep' , '" . s:query . "')"
  call VffListBufs ("grep")
endfunction

function! VffGetSelection()
  return getline('.')[col("'<")-1:col("'>")-1]
endfunction

function! VffClearSetup ()
  aug ListFiles
    exec "au! WinEnter " . g:vffWindowName
    exec "au! WinLeave " . g:vffWindowName
    exec "au! BufLeave " . g:vffWindowName
  aug END
  call VffUnsetupSelect ()
endfunction

function! VffSetupBadSelect ()
  if ! exists ("g:VffSetup")
    nnoremap <buffer> <CR>     :call VffQuit()<CR>
    nnoremap <buffer> <C-C>    :call VffQuit()<CR>
    " nnoremap <buffer> <ESC>    :call VffQuit()<CR>
    call VffSetupDeActivationKey ()
    let g:VffSetup = 1
  endif
endfunction

function! VffSetupSelect ()
  if ! exists ("g:VffSetup")
    set nofoldenable
    nnoremap <buffer> <CR>     :call VffSelectCurrentBuffer()<CR>
    nnoremap <buffer> <C-C>    :call VffQuit()<CR>
    " nnoremap <buffer> <ESC>    :call VffQuit()<CR>
    nnoremap <buffer> <SPACE>  :call VffText(' ')<CR>
    nnoremap <buffer> a        :call VffText('a')<CR>
    nnoremap <buffer> b        :call VffText('b')<CR>
    nnoremap <buffer> c        :call VffText('c')<CR>
    nnoremap <buffer> d        :call VffText('d')<CR>
    nnoremap <buffer> e        :call VffText('e')<CR>
    nnoremap <buffer> f        :call VffText('f')<CR>
    nnoremap <buffer> g        :call VffText('g')<CR>
    nnoremap <buffer> h        :call VffText('h')<CR>
    nnoremap <buffer> i        :call VffText('i')<CR>
    nnoremap <buffer> j        :call VffText('j')<CR>
    nnoremap <buffer> k        :call VffText('k')<CR>
    nnoremap <buffer> l        :call VffText('l')<CR>
    nnoremap <buffer> m        :call VffText('m')<CR>
    nnoremap <buffer> n        :call VffText('n')<CR>
    nnoremap <buffer> o        :call VffText('o')<CR>
    nnoremap <buffer> p        :call VffText('p')<CR>
    nnoremap <buffer> q        :call VffText('q')<CR>
    nnoremap <buffer> r        :call VffText('r')<CR>
    nnoremap <buffer> s        :call VffText('s')<CR>
    nnoremap <buffer> t        :call VffText('t')<CR>
    nnoremap <buffer> u        :call VffText('u')<CR>
    nnoremap <buffer> v        :call VffText('v')<CR>
    nnoremap <buffer> w        :call VffText('w')<CR>
    nnoremap <buffer> x        :call VffText('x')<CR>
    nnoremap <buffer> y        :call VffText('y')<CR>
    nnoremap <buffer> z        :call VffText('z')<CR>
    nnoremap <buffer> A        :call VffText('A')<CR>
    nnoremap <buffer> B        :call VffText('B')<CR>
    nnoremap <buffer> C        :call VffText('C')<CR>
    nnoremap <buffer> D        :call VffText('D')<CR>
    nnoremap <buffer> E        :call VffText('E')<CR>
    nnoremap <buffer> F        :call VffText('F')<CR>
    nnoremap <buffer> G        :call VffText('G')<CR>
    nnoremap <buffer> H        :call VffText('H')<CR>
    nnoremap <buffer> I        :call VffText('I')<CR>
    nnoremap <buffer> J        :call VffText('J')<CR>
    nnoremap <buffer> K        :call VffText('K')<CR>
    nnoremap <buffer> L        :call VffText('L')<CR>
    nnoremap <buffer> M        :call VffText('M')<CR>
    nnoremap <buffer> N        :call VffText('N')<CR>
    nnoremap <buffer> O        :call VffText('O')<CR>
    nnoremap <buffer> P        :call VffText('P')<CR>
    nnoremap <buffer> Q        :call VffText('Q')<CR>
    nnoremap <buffer> R        :call VffText('R')<CR>
    nnoremap <buffer> S        :call VffText('S')<CR>
    nnoremap <buffer> T        :call VffText('T')<CR>
    nnoremap <buffer> U        :call VffText('U')<CR>
    nnoremap <buffer> V        :call VffText('V')<CR>
    nnoremap <buffer> W        :call VffText('W')<CR>
    nnoremap <buffer> X        :call VffText('X')<CR>
    nnoremap <buffer> Y        :call VffText('Y')<CR>
    nnoremap <buffer> Z        :call VffText('Z')<CR>
    nnoremap <buffer> 0        :call VffText('0')<CR>
    nnoremap <buffer> 1        :call VffText('1')<CR>
    nnoremap <buffer> 2        :call VffText('2')<CR>
    nnoremap <buffer> 3        :call VffText('3')<CR>
    nnoremap <buffer> 4        :call VffText('4')<CR>
    nnoremap <buffer> 5        :call VffText('5')<CR>
    nnoremap <buffer> 6        :call VffText('6')<CR>
    nnoremap <buffer> 7        :call VffText('7')<CR>
    nnoremap <buffer> 8        :call VffText('8')<CR>
    nnoremap <buffer> 9        :call VffText('9')<CR>
    nnoremap <buffer> `        :call VffText('`')<CR>
    nnoremap <buffer> :        :call VffText(':')<CR>
    nnoremap <buffer> .        :call VffText('.')<CR>
    nnoremap <buffer> ,        :call VffText(',')<CR>
    nnoremap <buffer> ?        :call VffText('?')<CR>
    nnoremap <buffer> <        :call VffText('<')<CR>
    nnoremap <buffer> >        :call VffText('>')<CR>
    nnoremap <buffer> /        :call VffText('/')<CR>
    nnoremap <buffer> \        :call VffText('\\')<CR>
    nnoremap <buffer> !        :call VffText('!')<CR>
    nnoremap <buffer> @        :call VffText('@')<CR>
    nnoremap <buffer> #        :call VffText('#')<CR>
    nnoremap <buffer> $        :call VffText('$')<CR>
    nnoremap <buffer> %        :call VffText('%')<CR>
    nnoremap <buffer> ^        :call VffText('^')<CR>
    nnoremap <buffer> &        :call VffText('&')<CR>
    nnoremap <buffer> *        :call VffText('*')<CR>
    nnoremap <buffer> (        :call VffText('(')<CR>
    nnoremap <buffer> )        :call VffText(')')<CR>
    nnoremap <buffer> [        :call VffText('[')<CR>
    nnoremap <buffer> {        :call VffText('{')<CR>
    nnoremap <buffer> ]        :call VffText(']')<CR>
    nnoremap <buffer> }        :call VffText('}')<CR>
    nnoremap <buffer> -        :call VffText('-')<CR>
    nnoremap <buffer> _        :call VffText('_')<CR>
    nnoremap <buffer> +        :call VffText('+')<CR>
    nnoremap <buffer> =        :call VffText('=')<CR>
    nnoremap <buffer> "        :call VffText('"')<CR>
    nnoremap <buffer> ~        :call VffText('~')<CR>
    nnoremap <buffer> '        :call VffText('\''')<CR>
    nnoremap <buffer> \|       :call VffText("\|")<CR>
    nnoremap <buffer> <BS>     :call VffBackspace()<CR>
    nnoremap <buffer> <C-L>    :call VffClear()<CR>
    nnoremap <buffer> <M-J>    :call VffDown(1)<CR>
    nnoremap <buffer> <M-K>    :call VffUp(1)<CR>
    nnoremap <buffer> <A-J>    :call VffDown(1)<CR>
    nnoremap <buffer> <A-K>    :call VffUp(1)<CR>
    nnoremap <buffer> ∆        :call VffDown(1)<CR>
    nnoremap <buffer> ˚        :call VffUp(1)<CR>
    nnoremap <buffer> j      :call VffDown(1)<CR>
    nnoremap <buffer> k      :call VffUp(1)<CR>
    nnoremap <buffer> <C-DOWN> :call VffDown(1)<CR>
    nnoremap <buffer> <C-UP>   :call VffUp(1)<CR>
    nnoremap <buffer> <A-DOWN> :call VffDown(1)<CR>
    nnoremap <buffer> <A-UP>   :call VffUp(1)<CR>
    nnoremap <buffer> <S-DOWN> :call VffDown(1)<CR>
    nnoremap <buffer> <S-UP>   :call VffUp(1)<CR>
    nnoremap <buffer> <C-J>    :call VffDown(1)<CR>
    nnoremap <buffer> <C-K>    :call VffUp(1)<CR>
    nnoremap <buffer> <DOWN>   :call VffDown(1)<CR>
    nnoremap <buffer> <UP>     :call VffUp(1)<CR>
    nnoremap <buffer> <C-U>    :call VffUp(10)<CR>
    nnoremap <buffer> <C-D>    :call VffDown(10)<CR>
    cabbr <buffer> w q
    cabbr <buffer> wq q
    call VffSetupDeActivationKey ()
    let g:VffSetup = 1
  endif
endfunction

exec 'ruby load "' . expand('<sfile>:p:h') . '/VFF/VFF.rb"'
exec 'ruby $vff = VFF.new()'

if exists("g:vff_refreshdelay")
  exec "set updatetime=" . g:vff_refreshdelay
  " this autocommand fires when a char hasn't been typed in 'updatetime' ms, in normal mode
  autocmd CursorHold * :call VffRefresh()
endif

function! VffRefresh ()
  if exists("g:vff_needrefresh")
    if exists("g:vff_refreshdelay")
      exec "ruby $vff.refresh('" . g:vff_mode . "')"
      call VffGoToFirstResult ()
    endif
    unlet g:vff_needrefresh
  endif
endfunction

function! VffSaveLineNumber ()
  if g:vff_mode == "find"
    let g:vff_findlastline = line(".")
  elseif g:vff_mode == "grep"
    let g:vff_greplastline = line(".")
  endif
endfunction

" updates the entry line immediately but don't refresh the results until the next CursorHold event
function! VffText (ch)
  exec "ruby $vff.text_append('" . g:vff_mode . "' , '" . a:ch . "')"
  call VffSaveLineNumber ()
  echo ""
  if exists("g:vff_refreshdelay")
    let g:vff_needrefresh = 1
  else
    exec "ruby $vff.refresh('" . g:vff_mode . "')"
  endif
  call VffGoToFirstResult ()
endfunction

" updates the entry line immediately but don't refresh the results until the next CursorHold event
function! VffBackspace ()
  exec "ruby $vff.text_backspace('" . g:vff_mode . "')"
  call VffSaveLineNumber ()
  echo ""
  if exists("g:vff_refreshdelay")
    let g:vff_needrefresh = 1
  else
    exec "ruby $vff.refresh('" . g:vff_mode . "')"
  endif
  call VffGoToFirstResult ()
endfunction

" updates the entry and results immediately
function! VffClear ()
  exec "ruby $vff.text_clear('" . g:vff_mode . "')"
  call VffSaveLineNumber ()
  echo ""
  call VffGoToFirstResult ()
endfunction

function! VffUp(v)
  let l:line = line(".")
  if l:line - a:v > g:firstResultLineNumber
    silent! exec "normal! " . a:v . "k"
  else
    call VffGoToFirstResult ()
  endif
  call VffSaveLineNumber ()
  echo ""
endfunction

function! VffDown(v)
  silent! exec "normal! " . a:v . "j"
  call VffSaveLineNumber ()
  echo ""
endfunction

function! VffGoToFirstResult()
  exec g:firstResultLineNumber
endfunction

function! VffUnsetupSelect ()
  if exists ("g:VffSetup")
    call VffSetupActivationKey ()
    unlet g:VffSetup
  endif
endfunction

function! VffSelectCurrentBuffer ()
  let &timeoutlen = g:vff_savetimeoutlen
  let l:myBufNr = bufnr ("%")
  let l:line = getline(".")
  let l:lineNr = line(".")
  quit
  if l:line != "" && l:lineNr >= g:firstResultLineNumber
    exec 'ruby $vff.relativepath("' . getcwd() . '", "/' . substitute(l:line, "([0-9]\\+):.*", "", "") . '")'
    silent exec "edit " . fnameescape(g:vffrubyret)
    if g:vff_mode == 'grep'
      let l:offset = substitute(l:line, "^[^(]*(\\([0-9]\\+\\)):.*", "\\1", "")
      exec 'goto ' . l:offset
    endif
  endif
  if g:vffRemoveBrowserBuffer
    silent! exec "bd " . l:myBufNr
  endif
endfunction

function! VffDeActivate (mode)
  call VffQuit()
  if a:mode != g:vff_mode
    " Toggle between find/grep modes
    call VffListBufs (a:mode)
  else
    echo ""
  endif
endfunction

function! VffQuit ()
  let &timeoutlen = g:vff_savetimeoutlen
  let l:myBufNr = bufnr ("%")
  silent! exec "bd " . l:myBufNr
  call VffUnsetupSelect()
endfunction

function! VffChooseConfig ()
  call fzf#run({'source': 'ls .vff*', 'options': '--multi', 'sink': function("VffChangeConfig")})
endfunction

function! VffChangeConfig (configPath)
  exec "ruby $vff.change_config('" . getcwd() . "/" . a:configPath . "')"
endfunction
