# NASA imagery sources

All files are 4096x2048 equirectangular (2:1), JPEG quality 90, resized with Pillow (Lanczos) from the original listed below. Retrieved 2026-11-15.

Usage statement (NASA Images and Media Usage Guidelines, https://www.nasa.gov/nasa-brand-center/images-and-media/):
"NASA content - images, audio, video, and media files used in the rendition of 3-dimensional models, such as texture maps and polygon data in any format - generally are not subject to copyright in the United States. You may use this material for educational or informational purposes, including photo collections, textbooks, public exhibits, computer graphical simulations and Internet Web pages. [...] NASA should be acknowledged as the source of the material."

The Earth Observatory download pages themselves carry no per-image licence text beyond the site-wide NASA usage guidelines above. The Blue Marble credit line below follows the standard Visible Earth / Earth Observatory credit for this product family and was not printed on the fetched download page.

## earth_day.jpg
- Product: Blue Marble: Next Generation with Topography and Bathymetry, July 2004 composite (Terra/MODIS)
- Page: https://science.nasa.gov/earth/earth-observatory/blue-marble-next-generation/base-topography-bathymetry/
- Direct download: https://assets.science.nasa.gov/content/dam/science/esd/eo/images/bmng/bmng-topography-bathymetry/july/world.topo.bathy.200407.3x21600x10800.jpg
- Original resolution: 21600x10800 (2 km/pixel), 27.2 MB; downscaled to 4096x2048
- Credit: NASA Earth Observatory image by Reto Stockli (land surface, shallow water, topography). Enhancements by Robert Simmon (ocean color, compositing, 3D globes, animation). Data and technical support: MODIS Land Group; MODIS Science Data Support Team; MODIS Atmosphere Group; MODIS Ocean Group. Additional data: USGS EROS Data Center (topography); USGS Terrestrial Remote Sensing Flagstaff Field Center (Antarctica); Defense Meteorological Satellite Program (city lights). Short form: "NASA Earth Observatory".

## earth_night.jpg
- Product: Black Marble 2016 color global map (Suomi NPP VIIRS), 3 km
- Page: https://science.nasa.gov/earth/earth-observatory/earth-at-night/maps/
- Direct download: https://assets.science.nasa.gov/content/dam/science/esd/eo/images/imagerecords/144000/144898/BlackMarble_2016_3km.jpg
- Original resolution: 13500x6750 (geographic / equirectangular), 8.1 MB; downscaled to 4096x2048
- Credit (from the related NASA article https://science.nasa.gov/earth/earth-observatory/night-light-maps-open-up-new-applications-90008/): "NASA Earth Observatory images by Joshua Stevens, using Suomi NPP VIIRS data from Miguel Roman, NASA GSFC."

## earth_clouds.jpg - NOT PROVIDED
The NASA cloud layer ("Blue Marble: Clouds", Visible Earth record 57747 / file `cloud_combined_2048.jpg`) is no longer served at its old Visible Earth URL (redirects to the Earth Observatory home page). The old host `eoimages.gsfc.nasa.gov` timed out, and the same path on `assets.science.nasa.gov` returns 404. No equivalent equirectangular cloud texture was found on the current NASA sites (the Earth Observatory "cloud fraction" maps are data products, not a visual cloud layer). The only copy found was an Internet Archive mirror (non-NASA host), which was not used per the NASA-sources-only rule.

## starmap.jpg
- Product: Deep Star Maps 2020, celestial-coordinate star map (Hipparcos-2, Tycho-2 and Gaia DR2 catalogs), released 2020-09-09, visualization by Ernie Wright
- Page: https://svs.gsfc.nasa.gov/4851
- Direct download: https://svs.gsfc.nasa.gov/vis/a000000/a004800/a004851/starmap_2020_4k.exr
- Original resolution: 4096x2048 OpenEXR (half float, linear, max value 1.0), 34.3 MB. Converted to 8-bit JPEG with the sRGB transfer function (no exposure change). Orientation: celestial coordinates (equatorial), 0h RA at image centre, RA increasing to the left; the `_gal` variants on the same page are in galactic coordinates.
- Credit (verbatim from the page): "Please give credit for this item to: NASA/Goddard Space Flight Center Scientific Visualization Studio. Gaia DR2: ESA/Gaia/DPAC."
