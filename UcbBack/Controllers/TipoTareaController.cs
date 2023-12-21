using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using UcbBack.Models;

namespace UcbBack.Controllers
{
    public class TipoTareaController : ApiController
    {
        private ApplicationDbContext _context;

        public TipoTareaController()
        {
            _context = new ApplicationDbContext();
        }

        public IHttpActionResult Get()
        {
            //var tipoTarea = _context.TipoTarea.ToList();
            var tipoTarea = _context.TipoTarea.Select(x =>
                new
                {
                    x.Id,
                    x.Abr,
                    x.Tarea
                }).OrderBy(x => x.Abr);
            return Ok(tipoTarea);
        }
        public IHttpActionResult Get(int id)
        {
            TipoTarea positionInDB = null;

            positionInDB = _context.TipoTarea.FirstOrDefault(d => d.Id == id);

            if (positionInDB == null)
                return NotFound();

            return Ok(positionInDB);
        }

        [HttpPost]
        public IHttpActionResult Post([FromBody]TipoTarea tarea)
        {
            if (!ModelState.IsValid)
                return BadRequest();
            tarea.Id = TipoTarea.GetNextId(_context);
            _context.TipoTarea.Add(tarea);
            _context.SaveChanges();
            return Created(new Uri(Request.RequestUri + "/" + tarea.Id), tarea);
        }

        // PUT api/Positions/5
        [HttpPut]
        public IHttpActionResult Put(int id, [FromBody]TipoTarea tarea)
        {
            if (!ModelState.IsValid)
                return BadRequest();

            TipoTarea positionInDB = _context.TipoTarea.FirstOrDefault(d => d.Id == id);
            if (positionInDB == null)
                return NotFound();

            positionInDB.Tarea = tarea.Tarea;
            positionInDB.Abr = tarea.Abr;
            _context.SaveChanges();
            return Ok(positionInDB);
        }
        [HttpDelete]
        public IHttpActionResult DeleteRecord(int id)
        {
            //solo borrarlo en la primera instancia, no se eliminan los aprobados
            var recordForDeletion = _context.TipoTarea.FirstOrDefault(x => x.Id == id);
            if (recordForDeletion == null)
            {
                return BadRequest("El registro no existe en BD");
            }
            else
            {
                _context.TipoTarea.Remove(recordForDeletion);
                _context.SaveChanges();
                return Ok("Se eliminó el registro exitosamente");
            }
        }
    }
}