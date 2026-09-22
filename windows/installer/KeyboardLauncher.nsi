Unicode true
!include "MUI2.nsh"
!include "x64.nsh"
!include "LogicLib.nsh"
Name "${APP_NAME}"
OutFile "${OUTPUT_FILE}"
InstallDir "$LOCALAPPDATA\Programs\KeyboardLauncher"
InstallDirRegKey HKCU "Software\KeyboardLauncher" "InstallDir"
RequestExecutionLevel user
SetCompressor /SOLID lzma
SetCompressorDictSize 32
Icon "${APP_DIR}\Assets\AppIcon.ico"
UninstallIcon "${APP_DIR}\Assets\AppIcon.ico"
VIProductVersion "${APP_VERSION}.0"
VIAddVersionKey "ProductName" "${APP_NAME}"
VIAddVersionKey "FileDescription" "${APP_NAME} ${APP_ARCH} Setup"
VIAddVersionKey "FileVersion" "${APP_VERSION}"
VIAddVersionKey "LegalCopyright" "${APP_NAME}"
!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN "$INSTDIR\KeyboardLauncher.exe"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "SimpChinese"
Function .onInit
  !if "${APP_ARCH}" == "ARM64"
  ${IfNot} ${IsNativeARM64}
    MessageBox MB_ICONSTOP "This installer requires ARM64 Windows."
    Abort
  ${EndIf}
  !else
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP "This installer requires 64-bit Windows."
    Abort
  ${EndIf}
  !endif
  !insertmacro MUI_LANGDLL_DISPLAY
FunctionEnd
Section "${APP_NAME}"
  SetShellVarContext current
  SetOutPath "$INSTDIR"
  File /r /x *.pdb "${APP_DIR}\*"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\KeyboardLauncher.exe"
  WriteRegStr HKCU "Software\KeyboardLauncher" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\KeyboardLauncher" "DisplayName" "${APP_NAME}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\KeyboardLauncher" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\KeyboardLauncher" "DisplayIcon" "$INSTDIR\KeyboardLauncher.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\KeyboardLauncher" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\KeyboardLauncher" "NoModify" 1
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\KeyboardLauncher" "NoRepair" 1
SectionEnd
Section "Uninstall"
  SetShellVarContext current
  !include "${UNINSTALL_FILES}"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  Delete "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"
  RMDir "$SMPROGRAMS\${APP_NAME}"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\KeyboardLauncher"
  DeleteRegKey HKCU "Software\KeyboardLauncher"
  ; User configuration is deliberately preserved in LocalAppData\KeyboardLauncher.
SectionEnd
