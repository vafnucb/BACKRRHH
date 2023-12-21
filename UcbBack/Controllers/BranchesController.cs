using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Helpers;
using System.Web.Http;
using UcbBack.Models;
using System.Data.Entity;
using UcbBack.Logic;

namespace UcbBack.Controllers
{
    
    public class BranchesController : ApiController
    {
        private ApplicationDbContext _context;
        private ValidateAuth auth;

        public BranchesController()
        {
            _context = new ApplicationDbContext();
            auth = new ValidateAuth();
        }

        // GET api/Branches
        public IHttpActionResult Get()
        {
            var brs = _context.Branch.Include(x => x.Dependency).Select(x =>
                new {x.Id, 
                    x.Name, 
                    x.ADGroupName, 
                    x.ADOUName, 
                    x.Abr, 
                    Dependency = x.Dependency.Name,
                    x.InitialsInterRegional,
                    x.SerieComprobanteContalbeSAP,
                    x.SocioGenericDerechosLaborales,
                    x.InicialSN,
                    x.CodigoSAP,
                    x.CuentaSociosRCUNI,
                    x.CuentaSociosHCUNI,
                    x.VatGroup
                }).OrderBy(x=>x.Name);

            var user = auth.getUser(Request);
            var res = auth.filerByRegional(brs, user,isBranchtable:true);
            return Ok(res); 
        }

        // GET api/Branches/5
        public IHttpActionResult Get(int id)
        {
            Branches branchInDB = null;

            branchInDB = _context.Branch.FirstOrDefault(d => d.Id == id);
            
            if (branchInDB == null)
                return NotFound();

            return Ok(branchInDB);
        }

        // POST api/Branches
        [HttpPost]
        public IHttpActionResult Post([FromBody]Branches branch)
        {
            if (!ModelState.IsValid)
                return BadRequest();
            branch.Id = Branches.GetNextId(_context);

            _context.Branch.Add(branch);
            _context.SaveChanges();
            return Created(new Uri(Request.RequestUri + "/" + branch.Id), branch);
        }

        // PUT api/Branches/5
        [HttpPut]
        public IHttpActionResult Put(int id, [FromBody]Branches branch)
        {
            if (!ModelState.IsValid)
                return BadRequest();

            Branches brachInDB = _context.Branch.FirstOrDefault(d => d.Id == id);
            if (brachInDB == null)
                return NotFound();

            brachInDB.Name = branch.Name;
            brachInDB.Abr = branch.Abr;
            brachInDB.ADGroupName = branch.ADGroupName;
            brachInDB.ADOUName = branch.ADOUName;
            brachInDB.DependencyId = branch.DependencyId;
            brachInDB.InitialsInterRegional = branch.InitialsInterRegional;
            brachInDB.SerieComprobanteContalbeSAP = branch.SerieComprobanteContalbeSAP;
            brachInDB.SocioGenericDerechosLaborales = branch.SocioGenericDerechosLaborales;
            brachInDB.CodigoSAP = branch.CodigoSAP;
            brachInDB.InicialSN = branch.InicialSN;
            brachInDB.CuentaSociosHCUNI = branch.CuentaSociosHCUNI;
            brachInDB.CuentaSociosRCUNI = branch.CuentaSociosRCUNI;
            brachInDB.VatGroup = branch.VatGroup;
            _context.SaveChanges();
            return Ok(brachInDB);
        }

        // DELETE api/Branches/5
        [HttpDelete]
        public IHttpActionResult Delete(int id)
        {
            var brachInDB = _context.Branch.FirstOrDefault(d => d.Id == id);
            if (brachInDB == null)
                return NotFound();
            _context.Branch.Remove(brachInDB);
            _context.SaveChanges();
            return Ok();
        }
    }
}
