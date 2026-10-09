// The current API accepts an image URL rather than a multipart upload.
// Keep enough source resolution for the 300px-wide profile page while still
// reducing large uploads before they are stored as a data URL.
export async function profileImageDataUrl(file: File): Promise<string>
{
  const url = URL.createObjectURL(file)
  try {
    const image = new Image()
    image.src = url
    await image.decode()

    const maxWidth = 1200
    const maxHeight = 1000
    const scale = Math.min(1, maxWidth / image.width, maxHeight / image.height)

    const canvas = document.createElement('canvas')
    canvas.width = Math.max(1, Math.round(image.width * scale))
    canvas.height = Math.max(1, Math.round(image.height * scale))

    const context = canvas.getContext('2d')
    if (!context) throw new Error('Could not prepare your profile image.')

    context.imageSmoothingEnabled = true
    context.imageSmoothingQuality = 'high'
    context.drawImage(image, 0, 0, canvas.width, canvas.height)

    return canvas.toDataURL('image/webp', 0.92)
  } finally {
    URL.revokeObjectURL(url)
  }
}
