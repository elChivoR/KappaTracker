using Microsoft.AspNetCore.Mvc;
using KappaTracker.Services;
using KappaTracker.Models;
using SPTarkov.Common.Models.Logging;

namespace KappaTracker.Web.Controllers
{
    [ApiController]
    [Route("kappa/api")]
    public class ApiController : ControllerBase
    {
        private readonly ISptLogger<ApiController> _logger;
        private readonly KappaService _kappaService;
        private readonly TraderService _traderService;
        private readonly QuestService _questService;
        private readonly ProfileService _profileService;

        public ApiController(
            ISptLogger<ApiController> logger,
            KappaService kappaService,
            TraderService traderService,
            QuestService questService,
            ProfileService profileService)
        {
            _logger = logger;
            _kappaService = kappaService;
            _traderService = traderService;
            _questService = questService;
            _profileService = profileService;
        }

        [HttpGet("profiles")]
        public ActionResult<List<ProfileSummary>> GetProfiles()
        {
            try
            {
                return Ok(_profileService.GetAllProfileSummaries());
            }
            catch (Exception ex)
            {
                _logger.Error("[KappaTracker] Error getting profiles", ex);
                return StatusCode(500, new { error = "Failed to get profiles" });
            }
        }

        [HttpGet("progress")]
        public ActionResult<KappaProgressViewModel> GetProgress([FromQuery] string? profile = null)
        {
            try
            {
                return Ok(_kappaService.GetKappaProgress(profile));
            }
            catch (Exception ex)
            {
                _logger.Error("[KappaTracker] Error getting Kappa progress", ex);
                return StatusCode(500, new { error = "Failed to get Kappa progress" });
            }
        }

        [HttpGet("milestones")]
        public ActionResult GetMilestones()
        {
            try
            {
                var templateIds = _questService.GetKappaQuestIds();
                return Ok(new
                {
                    modVersion = ModInfo.Version,
                    fallback = _questService.KappaQuestIdsAreFallback,
                    templateIds,
                });
            }
            catch (Exception ex)
            {
                _logger.Error("[KappaTracker] Error getting milestone quest ids", ex);
                return StatusCode(500, new { error = "Failed to get milestones" });
            }
        }

        [HttpGet("traders")]
        public ActionResult<List<TraderViewModel>> GetTraders([FromQuery] string? profile = null)
        {
            try
            {
                return Ok(_traderService.GetAllTraders(profile));
            }
            catch (Exception ex)
            {
                _logger.Error("[KappaTracker] Error getting traders", ex);
                return StatusCode(500, new { error = "Failed to get traders" });
            }
        }

        [HttpGet("quests/{traderId}")]
        public ActionResult<List<QuestViewModel>> GetQuestsByTrader(string traderId, [FromQuery] string? profile = null)
        {
            try
            {
                return Ok(_questService.GetQuestsByTrader(traderId, profileId: profile));
            }
            catch (Exception ex)
            {
                _logger.Error($"[KappaTracker] Error getting quests for trader {traderId}", ex);
                return StatusCode(500, new { error = "Failed to get quests" });
            }
        }

        [HttpGet("quest/{questId}")]
        public ActionResult<QuestViewModel> GetQuestDetail(string questId, [FromQuery] string? profile = null)
        {
            try
            {
                var detail = _questService.GetQuestDetail(questId, profile);
                return detail is null ? NotFound() : Ok(detail);
            }
            catch (Exception ex)
            {
                _logger.Error($"[KappaTracker] Error getting quest detail {questId}", ex);
                return StatusCode(500, new { error = "Failed to get quest detail" });
            }
        }
    }
}
