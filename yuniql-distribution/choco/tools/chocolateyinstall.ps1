$packageName = $env:ChocolateyPackageName
$version = $env:ChocolateyPackageVersion
$toolsDir = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$packageUrl = "https://github.com/rdagumampan/yuniql/releases/download/v$version/yuniql-cli-win-x64-v$version.zip"
$packageArgs = @{
    packageName     = $packageName
    url             = $packageUrl
    unzipLocation   =  $toolsDir
    checksum        = '1A268CA9E5F0FA8B6D83B84A6C34F2B6C9FBDFB93867304BB7A92B059CF3A63E'
    checksumType    = 'sha256'
}

Install-ChocolateyZipPackage `
  -PackageName $packageArgs.packageName `
  -Url $packageArgs.url `
  -UnzipLocation $packageArgs.unzipLocation `
  -Checksum $packageArgs.checksum `
  -ChecksumType $packageArgs.checksumType

$installPath = "$toolsDir\$packageName-$version\yuniql.exe"
$packageArgs = @{
    name    = $packageName
    path    = $installPath
}

Install-BinFile `
  -Name $packageArgs.name `
  -Path $packageArgs.path