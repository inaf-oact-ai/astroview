import os
import sys
import numpy as np
from astropy.io import fits
from astropy.stats import sigma_clipped_stats
from astropy.stats import sigma_clip
from astropy.visualization import ZScaleInterval
from skimage.morphology import disk
from skimage.filters import rank
from skimage.exposure import equalize_adapthist, adjust_sigmoid, adjust_log, rescale_intensity, equalize_hist
from sklearn.preprocessing import minmax_scale
from PIL import Image

import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt

import concurrent.futures

def extract_png(inputfile, outputfile):

    data = fits.open(inputfile, ignore_blank=False)[0].data
    cond = np.logical_and(data != 0, np.isfinite(data))
    data_1d = data[cond]

    # - Set NAN/inf pixels to 0
    data[~cond] = 0

    # --- Save blank image if all data is zero ---
    if np.all(data == 0):
        print(f"All data is zero after BLANK handling in {inputfile}. Saving blank image.")
        blank_img = np.zeros_like(data, dtype=np.uint8)
        img = Image.fromarray(np.flipud(blank_img))
        img.save(outputfile)
        return
    # --------------------------------------------

    # - Subtract background
    if subtract_bkg:
        sigma_bkg = 3
        bkgval, _, _ = sigma_clipped_stats(data_1d, sigma=sigma_bkg)
        data_bkgsub = data - bkgval
        data = data_bkgsub

    # - Clip all pixels that are below sigma clip
    if clip_data:
      cond= np.logical_and(data!=0, np.isfinite(data))
      data_1d= data[cond]
      res= sigma_clip(data_1d, sigma_lower=sigma_low, sigma_upper=sigma_up, masked=True, return_bounds=True)
      thr_low= float(res[1])
      thr_up= float(res[2])
      print("thr_low=%f, thr_up=%f" % (thr_low, thr_up))

      data_clipped= np.copy(data)
      data_clipped[data_clipped<thr_low]= thr_low
      data_clipped[data_clipped>thr_up]= thr_up
      data= data_clipped

    # - Apply Zscale
    if zscale_data:
      transform = ZScaleInterval(contrast=contrast)
      data_stretched = transform(data)
      data= data_stretched

    # - Apply min/max norm
    norm_min= 0.0
    norm_max= 1.0
    if apply_minmax:
      cond= np.logical_and(data!=0, np.isfinite(data))
      data_1d= data[cond]
      data_min= np.nanmin(data_1d)
      data_max= np.nanmax(data_1d)
      data_norm= (data-data_min)/(data_max-data_min) * (norm_max-norm_min) + norm_min
      data= data_norm

    # - Convert to PNG
    # - Save
    if use_pil:
      img = Image.fromarray(np.flipud(data * 255).astype(np.uint8))
      img.save(outputfile)
    else:
      fig, ax = plt.subplots()
      ax.axis('off')
      ax.imshow(data, origin='lower')
      fig.savefig(outputfile, bbox_inches='tight', transparent=False, pad_inches=0)
      plt.close(fig)  # Properly close the figure to avoid memory leaks

def process_file(inputfile):
    inputfile = inputfile.rstrip()
    name = os.path.basename(inputfile)
    outputfile = os.path.join(outputdir, name)
    outputfile = os.path.splitext(outputfile)[0] + '.png'
    exists = os.path.exists(outputfile)
    if exists and not replacefiles:
        return
    extract_png(inputfile, outputfile)

# - Read args
fits_list=sys.argv[1] # full path to a text file containing paths to the fits images
outputdir=sys.argv[2] # full path to a folder to save PNG files
replacefiles=sys.argv[3].lower() == 'true' # replace existing PNG files
contrast=float(sys.argv[4])
sigma_low=float(sys.argv[5]) 
sigma_up=float(sys.argv[6]) 
use_pil= sys.argv[7].lower() == 'true'
subtract_bkg= sys.argv[8].lower() == 'true'
clip_data= sys.argv[9].lower() == 'true'
zscale_data= sys.argv[10].lower() == 'true'
apply_minmax= sys.argv[11].lower() == 'true'

print('fits_list=', fits_list)
print('outputdir=', outputdir)
print('replacefiles=', replacefiles)
print('contrast=', contrast)
print('sigma_low=', sigma_low)
print('sigma_up=', sigma_up)
print('use_pil=', use_pil)
print('subtract_bkg=', subtract_bkg)
print('clip_data=', clip_data)
print('zscale_data=', zscale_data)
print('apply_minmax=', apply_minmax)

sys.stdout.flush()

f = open(fits_list) # Open file on read mode
files = f.readlines() # Read all lines
f.close() # Close file

file_count = len(files)

with concurrent.futures.ThreadPoolExecutor() as executor:
    futures = []
    for inputfile in files:
        futures.append(executor.submit(process_file, inputfile))
    for idx, future in enumerate(concurrent.futures.as_completed(futures), 1):
        if idx % 100 == 0 or idx == file_count:
            print(f"{idx} \\ {file_count}")
            sys.stdout.flush()

print(file_count, '\\', file_count)