from PIL import Image
import numpy as np

width, height = 1920*2, 1080  # Dimensions of the image
n = 100  # Number of pixels from the edge where the fade starts

# Create a new image with RGB + Alpha
img = Image.new('RGBA', (width, height), color = 'red')

pixels = np.array(img)

# Create gradient masks for the fade effect
for i in range(n):
    alpha = int((1 - ((n-i) / float(n))**2) * 255.0)
    for j in range(width/2):
        pixels[-(i+1), width/2 + j, 3] = min(pixels[-(i+1), width/2 + j, 3], alpha)
    for j in range(height):
        pixels[j, width/2 + i, 3] = min(pixels[j, width/2 + i, 3], alpha)

# Convert back to an image
img = Image.fromarray(pixels)

# Save the image
img.save('mask.png')
