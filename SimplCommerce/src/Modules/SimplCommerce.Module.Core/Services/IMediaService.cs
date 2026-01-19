using System.IO;
using System.Threading.Tasks;
using SimplCommerce.Module.Core.Models;

namespace SimplCommerce.Module.Core.Services
{
    public interface IMediaService
    {
        string GetMediaUrl(Media media);
        string GetMediaUrl(string fileName);

        // Standard thumbnail (No parameters)
        string GetThumbnailUrl(Media media);

        // Custom size thumbnail (Explicit parameter, NO default value)
        string GetThumbnailUrl(Media media, string size);

        Task SaveMediaAsync(Stream mediaBinaryStream, string fileName, string mimeType = null);
        Task DeleteMediaAsync(Media media);
        Task DeleteMediaAsync(string fileName);
    }
}
