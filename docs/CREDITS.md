# Image credits

All pet illustrations used in the UI (`src/MiMascota.Web/wwwroot/img/mascotas/`) were **generated with Figma AI** for this portfolio project. Their backgrounds were removed with [rembg](https://github.com/danielgatis/rembg), and they were then cropped and converted to WebP (with transparency) with Pillow. The round avatars in `img/mascotas/avatar/` are square crops of the same images.

The transparent PNG cutouts are kept in `assets/cutouts/` as the source for those WebP files. The original generated images with backgrounds are not versioned because of their size.

The owners, pets and appointments in the demo data are fictional.

Fonts: [Baloo 2](https://fonts.google.com/specimen/Baloo+2) and [Montserrat](https://fonts.google.com/specimen/Montserrat) (SIL Open Font License), self-hosted from the Fontsource packages. Icons: [Bootstrap Icons](https://icons.getbootstrap.com/) (MIT).


## Animation frames

`perro-pata-media`, `perro-pata-abajo` and `gato-parpadeo` are edits of the original puppy and kitten photos made with OpenAI's image model (gpt-image, through Codex). Only the paw or eye region of each edit is blended onto the original photo, so the frames line up.

## Exotic species

`cotorro`, `perico`, `tortuga`, `iguana` and `huron` were generated with OpenAI's image model (gpt-image, through Codex) in the same studio style, then cut out with rembg.

## Uploaded pet photos

Photos uploaded by staff are re-encoded on the server with [SkiaSharp](https://github.com/mono/SkiaSharp) (MIT): the file is fully decoded, rotated according to the camera orientation, scaled to 1000 px max and saved as WebP without metadata (no EXIF or GPS location).
