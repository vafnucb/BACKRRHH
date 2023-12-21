using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using UcbBack.Models;

namespace UcbBack.Controllers
{
    public class ModalidadesController : ApiController
    {
        private ApplicationDbContext _context;

        public ModalidadesController()
        {
            _context = new ApplicationDbContext();
        }

        public IHttpActionResult Get()
        {
            //var modalidades = _context.Modalidades.OrderBy(Name).ToList();
            var modalidades = _context.Modalidades.Select(x =>
                new
                {
                    x.Id,
                    x.Abr,
                    x.Modalidad
                }).OrderBy(x => x.Abr);
            return Ok(modalidades);
        }

        [HttpPost]
        public IHttpActionResult Post([FromBody]Modalidades modal)
        {
            if (!ModelState.IsValid)
                return BadRequest();
            modal.Id = Modalidades.GetNextId(_context);
            _context.Modalidades.Add(modal);
            _context.SaveChanges();
            return Created(new Uri(Request.RequestUri + "/" + modal.Id), modal);
        }
        public IHttpActionResult Get(int id)
        {
            Modalidades positionInDB = null;

            positionInDB = _context.Modalidades.FirstOrDefault(d => d.Id == id);

            if (positionInDB == null)
                return NotFound();

            return Ok(positionInDB);
        }
        // PUT api/Positions/5
        [HttpPut]
        public IHttpActionResult Put(int id, [FromBody]Modalidades modal)
        {
            if (!ModelState.IsValid)
                return BadRequest();

            Modalidades positionInDB = _context.Modalidades.FirstOrDefault(d => d.Id == id);
            if (positionInDB == null)
                return NotFound();

            positionInDB.Modalidad = modal.Modalidad;
            positionInDB.Abr = modal.Abr;
            _context.SaveChanges();
            return Ok(positionInDB);
        }
        [HttpDelete]
        public IHttpActionResult DeleteRecord(int id)
        {
            //solo borrarlo en la primera instancia, no se eliminan los aprobados
            var recordForDeletion = _context.Modalidades.FirstOrDefault(x => x.Id == id);
            if (recordForDeletion == null)
            {
                return BadRequest("El registro no existe en BD");
            }
            else
            {
                _context.Modalidades.Remove(recordForDeletion);
                _context.SaveChanges();
                return Ok("Se eliminó el registro exitosamente");
            }
        }
    }
}