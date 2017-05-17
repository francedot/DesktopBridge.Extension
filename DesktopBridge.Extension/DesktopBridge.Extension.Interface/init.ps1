param($installPath)

$projectPath = Split-Path (Get-Project).FileName
$deploySource = join-path $installPath 'tools/deploy'
$deployTargetX86Debug = "$projectPath\bin\x86\Debug\AppX\desktop\"
$deployTargetX64Debug = "$projectPath\bin\x64\Debug\AppX\desktop\"
$deployTargetX86Release = "$projectPath\bin\x86\Release\AppX\desktop\"
$deployTargetX64Release = "$projectPath\bin\x64\Release\AppX\desktop\"

# Debug
# [System.Reflection.Assembly]::LoadWithPartialName("System.Windows.Forms")
# $oReturn1=[System.Windows.Forms.Messagebox]::Show("project=" + $projectPath)
# $oReturn2=[System.Windows.Forms.Messagebox]::Show("deploySource=" + $deploySource)
# $oReturn3=[System.Windows.Forms.Messagebox]::Show("deployTarget=" + $deployTarget)

# create our deploy target directory if it doesn't exist yet
if (!(test-path $deployTargetX86Debug)) {
 	New-Item $deployTargetX86Debug -ItemType Directory > $null
}
if (!(test-path $deployTargetX64Debug)) {
 	New-Item $deployTargetX64Debug -ItemType Directory > $null
}
if (!(test-path $deployTargetX86Release)) {
 	New-Item $deployTargetX86Release -ItemType Directory > $null
}
if (!(test-path $deployTargetX64Release)) {
 	New-Item $deployTargetX64Release -ItemType Directory > $null
}

# copy everything in there
Copy-Item "$deploySource/*" $deployTargetX86Debug -Recurse -Force
Copy-Item "$deploySource/*" $deployTargetX64Debug -Recurse -Force
Copy-Item "$deploySource/*" $deployTargetX86Release -Recurse -Force
Copy-Item "$deploySource/*" $deployTargetX64Release -Recurse -Force