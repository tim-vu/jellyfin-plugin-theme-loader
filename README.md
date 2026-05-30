# Jellyfin Theme Loader

Theme Loader is a Jellyfin plugin that loads packaged CSS themes into Jellyfin Web.

A theme is a ZIP containing a manifest, an entrypoint CSS file, and any local assets referenced by that CSS. The plugin stores uploaded themes, serves their files from Jellyfin, and applies the selected theme when Jellyfin Web loads.

Because the stylesheet is added during the initial Jellyfin Web load, the selected theme will be loaded before the app is rendered even on slow connections.

## Installation

Install [File Transformation](https://github.com/IAmParadox27/jellyfin-plugin-file-transformation).

Add this Jellyfin plugin repository:

```text
https://jellyfin.vuegen.dev/plugins/manifest.json
```

Install the **Theme Loader** plugin, then restart Jellyfin.

To configure Theme Loader. Go to the plugins settings page.

## Theme ZIP format

A theme package is a `.zip` file with `theme.json` in the ZIP root:

```text
theme.zip
|-- theme.json
|-- css/
|   `-- index.css
|-- images/
|   `-- background.png
`-- fonts/
    `-- theme.woff2
```

`theme.json` must include `name`, `version`, and `entrypoint`:

```json
{
  "name": "My Theme",
  "version": "1.0.0",
  "entrypoint": "css/index.css"
}
```

`entrypoint` is the CSS file applied to Jellyfin Web. It can be in the root, such as `style.css`, or in a folder, such as `css/index.css`.

CSS is validated but not rewritten. Asset URLs must be relative to the CSS file that references them:

```css
@import url("components/buttons.css");

body {
  background-image: url("../images/background.png");
}

@font-face {
  font-family: "Theme";
  src: url("../fonts/theme.woff2") format("woff2");
}
```

Relative `@import` rules are allowed and validated recursively. Embedded `data:` URLs are allowed for assets, but not for CSS imports.

## Not allowed in theme packages

Theme Loader rejects packages with:

- missing `theme.json`, or `theme.json` outside the ZIP root;
- missing `name`, `version`, or `entrypoint`;
- missing entrypoint CSS;
- CSS references to files not included in the ZIP;
- remote CSS URLs, including `http://`, `https://`, `//`, and other absolute URL schemes;
- remote or embedded `@import` rules;
- root-relative URLs such as `/ThemeLoader/Assets/font.woff2` or `/images/bg.png`;
- absolute archive paths, Windows drive-style paths, or paths containing `:`;
- backslash path separators;
- paths that escape the theme root with `..`;
- unsafe version values such as `.`, `..`, or versions containing `/`, `\`, or `:`.
