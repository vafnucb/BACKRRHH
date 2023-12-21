using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using UcbBack.Logic;
using UcbBack.Models;

namespace UcbBack.Controllers
{
    public class CauseOfMovementController : ApiController
    {
        private ApplicationDbContext _context;
        private ValidateAuth auth;

        public CauseOfMovementController()
        {
            _context = new ApplicationDbContext();
            auth = new ValidateAuth();
        }

        [HttpGet]
        [Route("api/CauseOfMovement/Baja")]
        public IHttpActionResult GetBaja()
        {
            var causes = _context.CauseOfMovements.Where(x => x.Type == "B" && x.Enabled);
            return Ok(causes);
        }

        [HttpGet]
        [Route("api/CauseOfMovement/Movement")]
        public IHttpActionResult GetMovement()
        {
            var causes = _context.CauseOfMovements.Where(x => x.Type == "M" && x.Enabled);
            return Ok(causes);
        }
    }
}
