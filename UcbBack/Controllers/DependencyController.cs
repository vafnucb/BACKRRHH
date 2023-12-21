using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using UcbBack.Models;
using System.Data.Entity;
using UcbBack.Logic;

namespace UcbBack.Controllers
{
    public class DependencyController : ApiController
    {
        private ApplicationDbContext _context;
        private ValidateAuth validator;

        public DependencyController()
        {
            _context = new ApplicationDbContext();
            validator = new ValidateAuth();
        }

        // GET api/Level
        public IHttpActionResult Get()
        {
            //var deplist = _context.Dependencies.Include(p => p.OrganizationalUnit).Include(i => i.Parent).ToList().Select(x => new { x.Id, x.Cod, x.Name, OrganizationalUnit = x.OrganizationalUnit.Name, Parent = x.Parent.Name }).OrderBy(x => x.Cod);
            var deplist = (from dependency in _context.Dependencies
                join branch in _context.Branch on dependency.BranchesId equals branch.Id
                join OU in _context.OrganizationalUnits on dependency.OrganizationalUnitId equals OU.Id
                join parent in _context.Dependencies on dependency.ParentId equals parent.Id
                join performance in _context.PerformanceAreas on dependency.PerformanceAreaId equals performance.Id
                           select new { dependency.Id, dependency.Cod, dependency.Name, OrganizationalUnit = OU.Name, OUCod = OU.Cod, Parent = parent.Name, ParentCod = parent.Cod, Branch = branch.Abr, BranchesId = branch.Id, 
                               dependency.Active,dependency.Academic,dependency.PerformanceAreaId, PerformanceArea = performance.Name}
                    ).OrderBy(x => x.Cod);
            var user = validator.getUser(Request);
            validator.filerByRegional(deplist,user);
            return Ok(deplist);
        }

        // GET api/Level/5
        public IHttpActionResult Get(int id)
        {
            Dependency depInDB = null;

            depInDB = _context.Dependencies.FirstOrDefault(d => d.Id == id);

            if (depInDB == null)
                return NotFound();

            return Ok(depInDB);
        }

        // POST api/Level
        [HttpPost]
        public IHttpActionResult Post([FromBody]Dependency dependency)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            dependency.Id = Dependency.GetNextId(_context);
            dependency.Name = dependency.Name.ToUpper();
            _context.Dependencies.Add(dependency);
            _context.SaveChanges();
            return Created(new Uri(Request.RequestUri + "/" + dependency.Id), dependency);
        }

        // PUT api/Level/5
        [HttpPut]
        public IHttpActionResult Put(int id, [FromBody]Dependency dependency)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            Dependency depInDB = _context.Dependencies.FirstOrDefault(d => d.Id == id);
            if (depInDB == null)
                return NotFound();

            depInDB.Cod = dependency.Cod;
            depInDB.Name = dependency.Name;
            depInDB.ParentId = dependency.ParentId;
            depInDB.OrganizationalUnitId = dependency.OrganizationalUnitId;
            depInDB.BranchesId = dependency.BranchesId;
            depInDB.Active = dependency.Active;
            depInDB.Academic = dependency.Academic;
            depInDB.PerformanceAreaId = dependency.PerformanceAreaId;
            // depInDB.Branches = _context.Branch.FirstOrDefault(x => x.Id == dependency.BranchesId);

            _context.SaveChanges();
            return Ok(depInDB);
        }

        // DELETE api/Level/5
        [HttpDelete]
        public IHttpActionResult Delete(int id)
        {
            var depInDB = _context.Dependencies.FirstOrDefault(d => d.Id == id);
            if (depInDB == null)
                return NotFound();
            _context.Dependencies.Remove(depInDB);
            _context.SaveChanges();
            return Ok();
        }
    }
}
