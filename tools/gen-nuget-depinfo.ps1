$ErrorActionPreference = 'Stop'

nuget-license -i ./ClassIsland.Desktop/ClassIsland.Desktop.csproj -t -o Json -fo ./ClassFabric/Assets/dependencies.g.json
