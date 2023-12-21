using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using UcbBack.Models;

namespace UcbBack.Controllers
{
    public class TableOfTablesController : ApiController
    {
        private ApplicationDbContext _context;
        public TableOfTablesController()
        {
            this._context = new ApplicationDbContext();
        }

        //-------------------------       Likage       ---------------------------------
        [HttpGet]
        [Route("api/TableOfTables/Linkage")]
        public IHttpActionResult GetLinkages()
        {
            return Ok(getFromTableOfTables(TableOfTablesTypes.Linkage).Select(x => new { x.Id, Name = x.Value }).OrderBy(x=>x.Name));
        }

        [HttpGet]
        [Route("api/TableOfTables/Linkage/{id}")]
        public IHttpActionResult GetLinkage(int id)
        {
            return Ok(getFromTableOfTables(TableOfTablesTypes.Linkage, id: id).FirstOrDefault());
        }

        [HttpPost]
        [Route("api/TableOfTables/Linkage/")]
        public IHttpActionResult AddLinkage(TableOfTables ToT)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }
            addToTableOfTables(TableOfTablesTypes.Linkage, ToT.Value);
            return Ok();
        }

        [HttpPut]
        [Route("api/TableOfTables/Linkage/{id}")]
        public IHttpActionResult UpdateLinkage(int id, TableOfTables ToT)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            return updateInTableOfTables(id, TableOfTablesTypes.Linkage, ToT.Value);
        }

        [HttpDelete]
        [Route("api/TableOfTables/Linkage/{id}")]
        public IHttpActionResult DeleteLinkage(int id)
        {
            return deleteInTableOfTables(id, TableOfTablesTypes.Linkage);
        }

        //-------------------------       AFP       ---------------------------------
        [HttpGet]
        [Route("api/TableOfTables/AFP")]
        public IHttpActionResult GetAFP()
        {
            return Ok(getFromTableOfTables(TableOfTablesTypes.AFP));
        }

        [HttpGet]
        [Route("api/TableOfTables/AFP/{id}")]
        public IHttpActionResult GetAFP(int id)
        {
            return Ok(getFromTableOfTables(TableOfTablesTypes.AFP, id: id).FirstOrDefault());
        }

        [HttpPost]
        [Route("api/TableOfTables/AFP/")]
        public IHttpActionResult AddAFP(TableOfTables ToT)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }
            addToTableOfTables(TableOfTablesTypes.AFP, ToT.Value);
            return Ok();
        }

        [HttpPut]
        [Route("api/TableOfTables/AFP/{id}")]
        public IHttpActionResult UpdateAFP(int id, TableOfTables ToT)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            return updateInTableOfTables(id, TableOfTablesTypes.AFP, ToT.Value);
        }

        [HttpDelete]
        [Route("api/TableOfTables/AFP/{id}")]
        public IHttpActionResult DeleteAFP(int id)
        {
            return deleteInTableOfTables(id, TableOfTablesTypes.AFP);
        }


        //-------------------------       FileState       ---------------------------------
        [HttpGet]
        [Route("api/TableOfTables/FileState")]
        public IHttpActionResult GetFileState()
        {
            return Ok(getFromTableOfTables(TableOfTablesTypes.FileState));
        }

        [HttpGet]
        [Route("api/TableOfTables/FileState/{id}")]
        public IHttpActionResult GetFileStateP(int id)
        {
            return Ok(getFromTableOfTables(TableOfTablesTypes.FileState, id: id).FirstOrDefault());
        }

        [HttpPost]
        [Route("api/TableOfTables/FileState/")]
        public IHttpActionResult AddFileState(TableOfTables ToT)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }
            addToTableOfTables(TableOfTablesTypes.FileState, ToT.Value);
            return Ok();
        }

        [HttpPut]
        [Route("api/TableOfTables/FileState/{id}")]
        public IHttpActionResult UpdateFileState(int id, TableOfTables ToT)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            return updateInTableOfTables(id, TableOfTablesTypes.FileState, ToT.Value);
        }

        [HttpDelete]
        [Route("api/TableOfTables/FileState/{id}")]
        public IHttpActionResult DeleteFileState(int id)
        {
            return deleteInTableOfTables(id, TableOfTablesTypes.FileState);
        }

        //-------------------------       CauseDesvinculation       ---------------------------------
        [HttpGet]
        [Route("api/TableOfTables/CauseDesvinculation")]
        public IHttpActionResult GetCauseDesvinculation()
        {
            return Ok(getFromTableOfTables(TableOfTablesTypes.CauseDesvinculation));
        }

        [HttpGet]
        [Route("api/TableOfTables/CauseDesvinculation/{id}")]
        public IHttpActionResult GetCauseDesvinculation(int id)
        {
            return Ok(getFromTableOfTables(TableOfTablesTypes.CauseDesvinculation, id: id).FirstOrDefault());
        }

        [HttpPost]
        [Route("api/TableOfTables/CauseDesvinculation/")]
        public IHttpActionResult AddCauseDesvinculation(TableOfTables ToT)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }
            addToTableOfTables(TableOfTablesTypes.CauseDesvinculation, ToT.Value);
            return Ok();
        }

        [HttpPut]
        [Route("api/TableOfTables/CauseDesvinculation/{id}")]
        public IHttpActionResult UpdateCauseDesvinculation(int id, TableOfTables ToT)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            return updateInTableOfTables(id, TableOfTablesTypes.CauseDesvinculation, ToT.Value);
        }

        [HttpDelete]
        [Route("api/TableOfTables/CauseDesvinculation/{id}")]
        public IHttpActionResult DeleteCauseDesvinculation(int id)
        {
            return deleteInTableOfTables(id, TableOfTablesTypes.CauseDesvinculation);
        }


        //-------------------------       Dedication       ---------------------------------
        [HttpGet]
        [Route("api/TableOfTables/Dedication")]
        public IHttpActionResult GetDedication()
        {
            return Ok(getFromTableOfTables(TableOfTablesTypes.Dedication));
        }

        [HttpGet]
        [Route("api/TableOfTables/Dedication/{id}")]
        public IHttpActionResult GetDedication(int id)
        {
            return Ok(getFromTableOfTables(TableOfTablesTypes.Dedication, id: id).FirstOrDefault());
        }

        [HttpPost]
        [Route("api/TableOfTables/Dedication/")]
        public IHttpActionResult AddDedication(TableOfTables ToT)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }
            addToTableOfTables(TableOfTablesTypes.Dedication, ToT.Value);
            return Ok();
        }

        [HttpPut]
        [Route("api/TableOfTables/Dedication/{id}")]
        public IHttpActionResult UpdateDedication(int id, TableOfTables ToT)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            return updateInTableOfTables(id, TableOfTablesTypes.Dedication, ToT.Value);
        }

        [HttpDelete]
        [Route("api/TableOfTables/Dedication/{id}")]
        public IHttpActionResult DeleteDedication(int id)
        {
            return deleteInTableOfTables(id, TableOfTablesTypes.Dedication);
        }

        //-------------------------       ProcessState       ---------------------------------
        [HttpGet]
        [Route("api/TableOfTables/ProcessState")]
        public IHttpActionResult GetProcessState()
        {
            return Ok(getFromTableOfTables(TableOfTablesTypes.ProcessState));
        }

        [HttpGet]
        [Route("api/TableOfTables/ProcessState/{id}")]
        public IHttpActionResult GetProcessState(int id)
        {
            return Ok(getFromTableOfTables(TableOfTablesTypes.ProcessState, id: id).FirstOrDefault());
        }

        [HttpPost]
        [Route("api/TableOfTables/ProcessState/")]
        public IHttpActionResult AddProcessState(TableOfTables ToT)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }
            addToTableOfTables(TableOfTablesTypes.ProcessState, ToT.Value);
            return Ok();
        }

        [HttpPut]
        [Route("api/TableOfTables/ProcessState/{id}")]
        public IHttpActionResult UpdateProcessState(int id, TableOfTables ToT)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            return updateInTableOfTables(id, TableOfTablesTypes.ProcessState, ToT.Value);
        }

        [HttpDelete]
        [Route("api/TableOfTables/ProcessState/{id}")]
        public IHttpActionResult DeleteProcessState(int id)
        {
            return deleteInTableOfTables(id, TableOfTablesTypes.ProcessState);
        }

        //-------------------------       TipoTarea       ---------------------------------

        [HttpGet]
        [Route("api/TableOfTables/TipoTarea")]
        public IHttpActionResult TipoTarea()
        {
            var Tipo = _context.TableOfTableses.Where(x => x.Id >= 34 && x.Id <= 40 || x.Id >= 54 && x.Id <= 56).OrderBy(x => x.Value);
            return Ok(Tipo);
        }

        //-------------------------       Modalidades       ---------------------------------
        [HttpGet]
        [Route("api/TableOfTables/Modalidades")]
        public IHttpActionResult Modalidades()
        {
            var Mods = _context.TableOfTableses.Where(x => x.Id >= 44 && x.Id <= 53 || x.Id == 57).OrderBy(x => x.Value);
            return Ok(Mods);
        }

        // delete
        [NonAction]
        private IHttpActionResult deleteInTableOfTables(int id, string type)
        {
            var ToTInDB = _context.TableOfTableses.FirstOrDefault(x => x.Type == type && x.Id == id);
            if (ToTInDB == null)
            {
                return NotFound();
            }

            _context.TableOfTableses.Remove(ToTInDB);
            return Ok();
        }

        // update
        [NonAction]
        private IHttpActionResult updateInTableOfTables(int id, string type, string value)
        {
            var ToTInDB = _context.TableOfTableses.FirstOrDefault(x => x.Type == type && x.Id == id);
            if (ToTInDB == null)
            {
                return NotFound();
            }

            ToTInDB.Value = value;
            _context.SaveChanges();
            return Ok();
        }

        // insert
        [NonAction]
        private void addToTableOfTables(string type, string value)
        {
            TableOfTables ToT = new TableOfTables();
            ToT.Id = TableOfTables.GetNextId(_context);
            ToT.Type = type;
            ToT.Value = value;
            _context.TableOfTableses.Add(ToT);
            _context.SaveChanges();
        }

        // get
        [NonAction]
        private List<TableOfTables> getFromTableOfTables(string type, int id = 0)
        {
            List<TableOfTables> res;
            if (id == 0)
            {
                res = _context.TableOfTableses.Where(x => x.Type == type).ToList();
            }
            else
            {
                res = _context.TableOfTableses.Where(x => x.Id == id && x.Type == type).ToList();
            }

            return res;
        }

    }
}
