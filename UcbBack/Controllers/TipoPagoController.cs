using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using UcbBack.Models;

namespace UcbBack.Controllers
{
    public class TipoPagoController : ApiController
    {
        private ApplicationDbContext _context;

        public TipoPagoController()
        {
            _context = new ApplicationDbContext();
        }

        [HttpGet]
        [Route("api/TipoPago")]
        public IHttpActionResult GetTipoPago()
        {
            var tipoPago = _context.TipoPago
                .Select(x => new
                {
                    x.Id,
                    x.Nombre
                })
                .OrderBy(x => x.Nombre)
                .ToList();

            return Ok(tipoPago);
        }
    }
}