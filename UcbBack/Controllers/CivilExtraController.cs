using System;
using System.Linq;
using System.Web.Http;
using UcbBack.Logic;
using UcbBack.Models;

namespace UcbBack.Controllers
{
    [RoutePrefix("api/CivilExtra")]
    public class CivilExtraController : ApiController
    {
        private readonly ApplicationDbContext _context;
        private readonly ValidateAuth auth;

        public CivilExtraController()
        {
            _context = new ApplicationDbContext();
            auth = new ValidateAuth();
        }

        // GET api/CivilExtra/{civilId}
        [HttpGet]
        [Route("{civilId}")]
        public IHttpActionResult Get(int civilId)
        {
            var user = auth.getUser(Request);
            if (user == null)
                return Unauthorized();

            var extra = _context.CivilExtras.FirstOrDefault(x => x.CivilId == civilId);

            if (extra == null)
            {
                return Ok(new
                {
                    CivilId = civilId,
                    BankName = "",
                    BankAccountNumber = ""
                });
            }

            return Ok(new
            {
                extra.CivilId,
                extra.BankName,
                extra.BankAccountNumber
            });
        }

        // POST api/CivilExtra/Update
        public class UpdateBankInfoRequest
        {
            public int CivilId { get; set; }
            public string BankName { get; set; }
            public string BankAccountNumber { get; set; }
        }

        [HttpPost]
        [Route("Update")]
        public IHttpActionResult Update([FromBody] UpdateBankInfoRequest request)
        {
            var user = auth.getUser(Request);
            if (user == null)
                return Unauthorized();

            if (request == null || request.CivilId <= 0)
                return BadRequest("Datos inválidos");

            // Verify civil exists
            var civil = _context.Civils.FirstOrDefault(c => c.Id == request.CivilId);
            if (civil == null)
                return NotFound();

            // Check if extra record exists
            var extra = _context.CivilExtras.FirstOrDefault(x => x.CivilId == request.CivilId);

            if (extra == null)
            {
                // Create new
                extra = new CivilExtra
                {
                    CivilId = request.CivilId,
                    BankName = (request.BankName ?? "").Trim(),
                    BankAccountNumber = (request.BankAccountNumber ?? "").Trim(),
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                _context.CivilExtras.Add(extra);
            }
            else
            {
                // Update existing
                extra.BankName = (request.BankName ?? "").Trim();
                extra.BankAccountNumber = (request.BankAccountNumber ?? "").Trim();
                extra.UpdatedAt = DateTime.Now;
            }

            _context.SaveChanges();

            return Ok(new
            {
                Message = "Datos bancarios actualizados correctamente",
                extra.CivilId,
                extra.BankName,
                extra.BankAccountNumber
            });
        }

        // GET api/CivilExtra/ByBranch/{branchId}
        // Returns bank info for all civils in a branch (for table display)
        [HttpGet]
        [Route("ByBranch/{branchId}")]
        public IHttpActionResult GetByBranch(int branchId)
        {
            var user = auth.getUser(Request);
            if (user == null)
                return Unauthorized();

            var civilIds = _context.Civils
                .Where(c => c.BranchesId == branchId || branchId == 0)
                .Select(c => c.Id)
                .ToList();

            var extras = _context.CivilExtras
                .Where(x => civilIds.Contains(x.CivilId))
                .Select(x => new
                {
                    x.CivilId,
                    x.BankName,
                    x.BankAccountNumber
                })
                .ToList();

            return Ok(extras);
        }
    }
}
