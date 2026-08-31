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

        public ApiController(
            ISptLogger<ApiController> logger,
            KappaService kappaService,
            TraderService traderService,
            QuestService questService)
        {
            _logger = logger;
            _kappaService = kappaService;
            _traderService = traderService;
            _questService = questService;
        }

        [HttpGet("progress")]
        public ActionResult<KappaProgressViewModel> GetProgress()
        {
            try
            {
                return Ok(_kappaService.GetKappaProgress());
            }
            catch (Exception ex)
            {
                _logger.Error("[KappaTracker] Error getting Kappa progress", ex);
                return StatusCode(500, new { error = "Failed to get Kappa progress" });
            }
        }

        [HttpGet("traders")]
        public ActionResult<List<TraderViewModel>> GetTraders()
        {
            try
            {
                return Ok(_traderService.GetAllTraders());
            }
            catch (Exception ex)
            {
                _logger.Error("[KappaTracker] Error getting traders", ex);
                return StatusCode(500, new { error = "Failed to get traders" });
            }
        }

        [HttpGet("quests/{traderId}")]
        public ActionResult<List<QuestViewModel>> GetQuestsByTrader(string traderId)
        {
            try
            {
                return Ok(_questService.GetQuestsByTrader(traderId));
            }
            catch (Exception ex)
            {
                _logger.Error($"[KappaTracker] Error getting quests for trader {traderId}", ex);
                return StatusCode(500, new { error = "Failed to get quests" });
            }
        }
    }
}
