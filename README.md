# Jellyfin Theme Loader

Jellyfin plugin for uploading one local theme package, serving its assets locally, and injecting the theme CSS into Jellyfin Web through the File Transformation plugin.

## Theme ZIP

Theme packages must contain:

- `theme.json` in the ZIP root directory
- A CSS entrypoint referenced by `theme.json`
- Optional local assets referenced from CSS with paths relative to the CSS file

Example `theme.json`:

```json
{
  "slug": "my-theme",
  "name": "My Theme",
  "version": "1.0.0",
  "entrypoint": "style.css"
}
```

CSS is validated, not rewritten. Theme CSS must reference assets with relative paths only, such as `fonts/font_a.woff` from a root `index.css`, or `../fonts/font_a.woff` from `css/index.css`.

Relative `@import` rules are allowed and validated recursively. Embedded `data:` asset URLs are allowed. Remote CSS assets (`http://`, `https://`, protocol-relative, and other absolute URLs), absolute paths, remote imports, embedded imports, and missing files are rejected.
