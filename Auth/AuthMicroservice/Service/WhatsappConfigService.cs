using AuthMicroservice.Model;
using AuthMicroservice.Repository;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuthMicroservice.Service
{
    public interface IWhatsappConfigService
    {
        Task<WhatsappConfig> GetWhatsappConfigAsync(Guid id);
        Task<IEnumerable<WhatsappConfig>> GetWhatsappConfigsAsync();
        Task<WhatsappConfig> CreateWhatsappConfigAsync(WhatsappConfig whatsappConfig);
        Task UpdateWhatsappConfigAsync(Guid id, WhatsappConfig whatsappConfig);
        Task<WhatsappConfig> GetWhatsappConfigByApplicationIdAsync(Guid applicationId);
    }

    public class WhatsappConfigService : IWhatsappConfigService
    {
        private readonly IWhatsappConfigRepository _whatsappConfigRepository;

        public WhatsappConfigService(IWhatsappConfigRepository whatsappConfigRepository)
        {
            _whatsappConfigRepository = whatsappConfigRepository;
        }

        public async Task<WhatsappConfig> GetWhatsappConfigAsync(Guid id)
        {
            return await _whatsappConfigRepository.GetByIdAsync(id);
        }

        public async Task<IEnumerable<WhatsappConfig>> GetWhatsappConfigsAsync()
        {
            return await _whatsappConfigRepository.GetAllAsync();
        }

        public async Task<WhatsappConfig> CreateWhatsappConfigAsync(WhatsappConfig whatsappConfig)
        {
            whatsappConfig.ConnectionStatus = "connected";
            await _whatsappConfigRepository.AddAsync(whatsappConfig);
            return whatsappConfig;
        }

        public async Task UpdateWhatsappConfigAsync(Guid id, WhatsappConfig whatsappConfig)
        {
            var existing = await _whatsappConfigRepository.GetByIdAsync(id);
            if (existing == null)
            {
                return;
            }
            existing.BusinessAccountId = whatsappConfig.BusinessAccountId;
            existing.PhoneNumberId = whatsappConfig.PhoneNumberId;
            existing.DisplayPhoneNumber = whatsappConfig.DisplayPhoneNumber;
            // Blank token on update means "keep the existing one" (frontend never re-displays it).
            if (!string.IsNullOrWhiteSpace(whatsappConfig.AccessToken))
            {
                existing.AccessToken = whatsappConfig.AccessToken;
                existing.ConnectionStatus = "connected";
            }
            await _whatsappConfigRepository.UpdateAsync(existing);
        }

        public async Task<WhatsappConfig> GetWhatsappConfigByApplicationIdAsync(Guid applicationId)
        {
            return await _whatsappConfigRepository.GetByApplicationIdAsync(applicationId);
        }
    }
}
