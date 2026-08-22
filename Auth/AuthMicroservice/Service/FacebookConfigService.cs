using AuthMicroservice.Model;
using AuthMicroservice.Repository;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuthMicroservice.Service
{
    public interface IFacebookConfigService
    {
        Task<FacebookConfig> GetFacebookConfigAsync(Guid id);
        Task<IEnumerable<FacebookConfig>> GetFacebookConfigsAsync();
        Task<FacebookConfig> CreateFacebookConfigAsync(FacebookConfig facebookConfig);
        Task UpdateFacebookConfigAsync(Guid id, FacebookConfig facebookConfig);
        Task<FacebookConfig> GetFacebookConfigByApplicationIdAsync(Guid applicationId);
    }

    public class FacebookConfigService : IFacebookConfigService
    {
        private readonly IFacebookConfigRepository _facebookConfigRepository;

        public FacebookConfigService(IFacebookConfigRepository facebookConfigRepository)
        {
            _facebookConfigRepository = facebookConfigRepository;
        }

        public async Task<FacebookConfig> GetFacebookConfigAsync(Guid id)
        {
            return await _facebookConfigRepository.GetByIdAsync(id);
        }

        public async Task<IEnumerable<FacebookConfig>> GetFacebookConfigsAsync()
        {
            return await _facebookConfigRepository.GetAllAsync();
        }

        public async Task<FacebookConfig> CreateFacebookConfigAsync(FacebookConfig facebookConfig)
        {
            facebookConfig.ConnectionStatus = "connected";
            await _facebookConfigRepository.AddAsync(facebookConfig);
            return facebookConfig;
        }

        public async Task UpdateFacebookConfigAsync(Guid id, FacebookConfig facebookConfig)
        {
            var existing = await _facebookConfigRepository.GetByIdAsync(id);
            if (existing == null)
            {
                return;
            }
            existing.PageId = facebookConfig.PageId;
            existing.PageName = facebookConfig.PageName;
            // Blank token on update means "keep the existing one" (frontend never re-displays it).
            if (!string.IsNullOrWhiteSpace(facebookConfig.PageAccessToken))
            {
                existing.PageAccessToken = facebookConfig.PageAccessToken;
                existing.ConnectionStatus = "connected";
            }
            await _facebookConfigRepository.UpdateAsync(existing);
        }

        public async Task<FacebookConfig> GetFacebookConfigByApplicationIdAsync(Guid applicationId)
        {
            return await _facebookConfigRepository.GetByApplicationIdAsync(applicationId);
        }
    }
}
