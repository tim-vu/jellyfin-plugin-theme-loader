dotnet build --configuration Release
dotnet publish
cp -f ./src/Jellyfin.Plugin.ThemeLoader/bin/Release/net9.0/publish/*.dll ~/.local/share/jellyfin/plugins/ThemeLoader/
