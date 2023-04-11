Loop
{
  game := ""
  IfWinExist, ahk_exe BONEWORKS.exe
  {
    game := "BONEWORKS"
  }
  IfWinExist, ahk_exe BONELAB_Oculus_Windows64.exe
  {
    game := "BONELAB"
  }
  
  If (game != "")
  {
    IfWinNotExist, ahk_exe obs64.exe
    {
      Run, "C:\Program Files\obs-studio\bin\64bit\obs64.exe" "--enable-gpu" "--enable-media-stream" "--startstreaming" "--startvirtualcam", C:\Program Files\obs-studio\bin\64bit
      WinWait, No Broadcast Configured, , 10
      If ErrorLevel
      {
        MsgBox, Could not find "No Broadcast Configured" window
      }
      Else
      {
        WinActivate, No Broadcast Configured
        Send, {Tab}{Tab}{Enter}
        WinWait, YouTube Broadcast Setup - Channel: jakzo, , 5
        If ErrorLevel
        {
          MsgBox, Could not find "YouTube Broadcast Setup" window
        }
        Else
        {
          WinActivate, YouTube Broadcast Setup - Channel: jakzo
          Send, +{Tab}{Enter}
          WinWaitClose, YouTube Broadcast Setup - Channel: jakzo, , 20
          Sleep 5000
          ; Run, "npm" "-w" "@jakzo/stream-notifier" "run" "start", %A_ScriptDir%/..
        }
      }
    }

    Loop
    {
      If !WinExist(GAME)
      {
        start := A_TickCount
        While !WinExist(GAME)
        {
          If A_TickCount - start >= 60000
            break
          Sleep 1000
        }
        If A_TickCount - start >= 60000
        {
          WinActivate, ahk_exe obs64.exe
          WinClose, ahk_exe obs64.exe
          WinWait, Exit OBS?, , 5
          If !ErrorLevel
          {
            WinActivate, Exit OBS?
            Send, {Tab}{Tab}{Enter}
          }
          WinWaitClose, ahk_exe obs64.exe, , 20
          If ErrorLevel
          {
            MsgBox, OBS did not close
          }
          break
        }
      }
      Sleep 1000
    }
  }
  Sleep 1000
}
