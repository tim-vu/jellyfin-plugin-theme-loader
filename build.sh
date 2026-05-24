dotnet publish
shopt -s extglob
mkdir ~/.local/share/jellyfin/plugins/ThemeLoader
cp -f src/Jellyfin.Plugin.ThemeLoader/bin/Release/net9.0/publish/!(Newtonsoft*.dll).dll ~/.local/share/jellyfin/plugins/ThemeLoader/
