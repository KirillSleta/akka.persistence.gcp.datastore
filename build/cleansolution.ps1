gci ../ -include bin, obj, Debug, Release, .vs -Recurse -Force -directory | Remove-Item -Recurse -Force
gci ../ -include *.user -Recurse -Force | Remove-Item -Recurse -Force