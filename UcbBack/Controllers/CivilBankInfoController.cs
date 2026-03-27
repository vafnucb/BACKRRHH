using System;
using System.Configuration;
using System.Linq;
using System.Web.Http;
using UcbBack.Logic;
using UcbBack.Models;
using UcbBack.Models.Auth;
using UcbBack.Models.Not_Mapped;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Controllers
{
    public class CivilBankInfoController : ApiController
    {
        private ApplicationDbContext _context;
        private ValidateAuth auth;
        private ADClass AD;

        public CivilBankInfoController()
        {
            _context = new ApplicationDbContext();
            auth = new ValidateAuth();
            AD = new ADClass();
        }

        public class CivilBankInfoGridDto
        {
            public int Id { get; set; }
            public string SAPId { get; set; }
            public string NIT { get; set; }
            public string FullName { get; set; }
            public string Document { get; set; }
            public int CreatedBy { get; set; }
            public int BranchesId { get; set; }
            public bool? IsEnabled { get; set; }   // comes from Civil
            public string Abr { get; set; }        // branch abbreviation
            public string BankName { get; set; }   // comes from twin table
            public string BankAccountNumber { get; set; } // comes from twin table
        }

        public class CivilBankInfoSaveDto
        {
            public int Id { get; set; }
            public string BankName { get; set; }
            public string BankAccountNumber { get; set; }
        }

        public class CivilChangeStatusDto
        {
            public int Id { get; set; }
            public bool IsEnabled { get; set; }
        }

        /// <summary>
        /// Returns Civil data + additional bank data from twin table
        /// </summary>
        [HttpGet]
        [Route("api/CivilBankInfobyBranch/{id}")]
        public IHttpActionResult CivilBankInfobyBranch(int id)
        {
            var user = auth.getUser(Request);

            if (id != 0)
            {
                var query = "select " +
                            " c.\"Id\", " +
                            " ocrd.\"CardName\" \"FullName\", " +
                            " c.\"SAPId\", " +
                            " c.\"NIT\", " +
                            " c.\"Document\", " +
                            " c.\"CreatedBy\", " +
                            " c.\"BranchesId\", " +
                            " c.\"IsEnabled\", " +
                            " br.\"Abr\" \"Abr\", " +
                            " cbi.\"BankName\" \"BankName\", " +
                            " cbi.\"BankAccountNumber\" \"BankAccountNumber\" " +
                            "\r\nfrom " + CustomSchema.Schema + ".\"Civil\" c" +
                            "\r\n inner join " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".ocrd" +
                            "   on ocrd.\"CardCode\" = c.\"SAPId\"" +
                            "\r\n inner join " + CustomSchema.Schema + ".\"Branches\" br" +
                            "   on br.\"Id\" = c.\"BranchesId\"" +
                            "\r\n left join " + CustomSchema.Schema + ".\"CivilBankInfo\" cbi" +
                            "   on cbi.\"Id\" = c.\"Id\"" +
                            "\r\n where ocrd.\"validFor\" = 'Y'" +
                            "\r\n   and ocrd.\"frozenFor\" = 'N'" +
                            "\r\n   and c.\"BranchesId\" = " + id +
                            "\r\n order by c.\"Id\";";

                var rawresult = _context.Database.SqlQuery<CivilBankInfoGridDto>(query);
                var res = auth.filerByRegional(rawresult.AsQueryable(), user);
                return Ok(res);
            }
            else
            {
                var brs = AD.getUserBranches(user);
                var brsIds = brs.Select(x => x.Id).ToList();

                if (!brsIds.Any())
                    return Ok(Enumerable.Empty<CivilBankInfoGridDto>());

                string StrIds = string.Join(", ", brsIds);

                var query = "select " +
                            " c.\"Id\", " +
                            " ocrd.\"CardName\" \"FullName\", " +
                            " c.\"SAPId\", " +
                            " c.\"NIT\", " +
                            " c.\"Document\", " +
                            " c.\"CreatedBy\", " +
                            " c.\"BranchesId\", " +
                            " c.\"IsEnabled\", " +
                            " br.\"Abr\" \"Abr\", " +
                            " cbi.\"BankName\" \"BankName\", " +
                            " cbi.\"BankAccountNumber\" \"BankAccountNumber\" " +
                            "\r\nfrom " + CustomSchema.Schema + ".\"Civil\" c" +
                            "\r\n inner join " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".ocrd" +
                            "   on ocrd.\"CardCode\" = c.\"SAPId\"" +
                            "\r\n inner join " + CustomSchema.Schema + ".\"Branches\" br" +
                            "   on br.\"Id\" = c.\"BranchesId\"" +
                            "\r\n left join " + CustomSchema.Schema + ".\"CivilBankInfo\" cbi" +
                            "   on cbi.\"Id\" = c.\"Id\"" +
                            "\r\n where ocrd.\"validFor\" = 'Y'" +
                            "\r\n   and ocrd.\"frozenFor\" = 'N'" +
                            "\r\n   and c.\"BranchesId\" in (" + StrIds + ")" +
                            "\r\n order by c.\"Id\";";

                var rawresult = _context.Database.SqlQuery<CivilBankInfoGridDto>(query);
                var res = auth.filerByRegional(rawresult.AsQueryable(), user);
                return Ok(res);
            }
        }

        /// <summary>
        /// Creates or updates the twin table record with bank info
        /// </summary>
        [HttpPost]
        [Route("api/CivilBankInfoSave")]
        public IHttpActionResult Save([FromBody] CivilBankInfoSaveDto request)
        {
            var user = auth.getUser(Request);

            if (!ModelState.IsValid)
                return BadRequest();

            if (request == null || request.Id <= 0)
                return BadRequest("Datos inválidos.");

            var civil = _context.Civils.FirstOrDefault(c => c.Id == request.Id);
            if (civil == null)
                return NotFound();

            var userBranches = AD.getUserBranches(user).Select(x => x.Id);
            if (!userBranches.Contains(civil.BranchesId))
                return Unauthorized();

            var existing = _context.Set<CivilBankInfo>().FirstOrDefault(x => x.Id == request.Id);

            if (existing == null)
            {
                var newRow = new CivilBankInfo
                {
                    Id = request.Id,
                    BankName = request.BankName,
                    BankAccountNumber = request.BankAccountNumber
                };

                _context.Set<CivilBankInfo>().Add(newRow);
            }
            else
            {
                existing.BankName = request.BankName;
                existing.BankAccountNumber = request.BankAccountNumber;
            }

            _context.SaveChanges();

            return Ok(new
            {
                request.Id,
                request.BankName,
                request.BankAccountNumber
            });
        }

        /// <summary>
        /// Changes IsEnabled in the original Civil table
        /// </summary>
        [HttpPost]
        [Route("api/CivilBankInfoChangeStatus")]
        public IHttpActionResult ChangeStatus([FromBody] CivilChangeStatusDto request)
        {
            var user = auth.getUser(Request);

            if (!ModelState.IsValid)
                return BadRequest();

            if (request == null || request.Id <= 0)
                return BadRequest("Datos inválidos.");

            var civil = _context.Civils.FirstOrDefault(c => c.Id == request.Id);
            if (civil == null)
                return NotFound();

            var userBranches = AD.getUserBranches(user).Select(x => x.Id);
            if (!userBranches.Contains(civil.BranchesId))
                return Unauthorized();

            civil.IsEnabled = request.IsEnabled;
            _context.SaveChanges();

            return Ok(new
            {
                civil.Id,
                civil.IsEnabled
            });
        }
    }
}
