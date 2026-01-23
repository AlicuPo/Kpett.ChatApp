using CloudinaryDotNet;
using CloudinaryDotNet.Actions;


namespace Kpett.ChatApp.Respository
{
    public class UploadFileRepository : Kpett.ChatApp.Services.ICloudinary
    {
        private readonly Cloudinary _cloudinary;
   
        public UploadFileRepository(Cloudinary cloudinary)
        {
            _cloudinary = cloudinary ?? throw new ArgumentNullException(nameof(cloudinary));
        }

        public async Task<string> UploadToCloudinaryAsync(IFormFile file, string basePath, string yearFolder, string monthFolder)
        {
            if (file == null || file.Length == 0) throw new ArgumentNullException(nameof(file));

            // 1. Tạo đường dẫn Folder (Ví dụ: avatars/2026/01)
            string remoteFolder = $"{basePath}/{yearFolder}/{monthFolder}";

            using var stream = file.OpenReadStream();

            // 2. Thiết lập thông số Upload
            var uploadParams = new ImageUploadParams()
            {
                File = new FileDescription(file.FileName, stream),
                Folder = remoteFolder,

                // TỐI ƯU CHO AVATAR: 
                // Tự động tìm khuôn mặt, cắt vuông 500x500, nén dung lượng mà không giảm chất lượng rõ rệt
                Transformation = new Transformation()
                    .Width(500).Height(500).Crop("thumb").Gravity("face").Quality("auto").FetchFormat("auto")
            };

            // 3. Thực thi Upload
            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.Error != null)
            {
                throw new Exception($"Cloudinary Upload Error: {uploadResult.Error.Message}");
            }

            // 4. Trả về URL (Cloudinary trả về HTTPS URL cực kỳ an toàn)
            return uploadResult.SecureUrl.ToString();
        }
    }
}
