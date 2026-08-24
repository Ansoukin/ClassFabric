$ErrorActionPreference = "Stop"

$appPath = "./out_pack/pack/app-${env:version}-0/"
$rootPath = "./out_pack/pack/"

if ($(Test-Path ./out_pack/) -eq $false) {
    mkdir -p $appPath
}

Get-ChildItem -Path ./out

$appBaseName = "out_appBase_${env:osName}_${env:arch}_${env:buildType}_folder"
$launcherName = "out_launcher_${env:osName}_${env:arch}_aot_singleFile"

Expand-Archive "./out/${appBaseName}.zip" -DestinationPath $appPath -Force
Expand-Archive "./out/${launcherName}.zip" -DestinationPath $rootPath -Force

Remove-Item $rootPath/*.pdb -Force
# 包内不写入任何外部分发体系的更新元数据，产物面向 GitHub Release 直接下载
