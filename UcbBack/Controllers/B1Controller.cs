using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using SAPbobsCOM;
using UcbBack.Logic.B1;
using UcbBack.Models;

namespace UcbBack.Controllers
{
    public class B1Controller : ApiController
    {
        private ApplicationDbContext _context;

        public B1Controller()
        {
            _context = new ApplicationDbContext();
        }

        // GET /B1
        public IHttpActionResult Get()
        {
            var B1con = B1Connection.Instance();
            // B1con.ConnectB1();
            // General error;258 insufficient privilege: Not authorized
            // var  id = B1con.addVoucher();
            // var  id = B1con.updatePersonInBP(_context.Person.FirstOrDefault(x => x.CUNI == "RFA940908"));
            // var  id = B1con.addPersonToB1(_context.Person.FirstOrDefault(x => x.CUNI == "RFA940908"));
            return Ok("  ****  "+B1con.getLastError());
        }

        [HttpGet]
        [Route("api/B1/BusinessPartners")]
        public IHttpActionResult BusinessPartners()
        {
            var B1con = B1Connection.Instance();
            return Ok(B1con.getBusinessPartners("*"));
        }
    }
}
