using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Migrations.History;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Http;
using UcbBack.Models;
using UcbBack.Logic;
using System.Globalization;
using System.Runtime.InteropServices;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Ajax.Utilities;
using Newtonsoft.Json.Linq;
using UcbBack.Logic.B1;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;
using UcbBack.Models.Not_Mapped.ViewMoldes;


namespace UcbBack.Controllers
{
    public class ContractController : ApiController
    {
         private ApplicationDbContext _context;
        private ValidateAuth auth;
        private B1Connection B1;
        private ADClass AD;

        public ContractController()
        {

            _context = new ApplicationDbContext();
            auth = new ValidateAuth();
            B1 = B1Connection.Instance();
            AD = new ADClass();

        }

        // GET api/Contract
        [Route("api/Contract")]
        public IHttpActionResult Get()
        {
            var query ="select * from " + CustomSchema.Schema + ".lastcontracts_PRIORITY "+
                        " where (\"Active\"=true or \"EndDate\">=current_date) ;";
            var rawresult = _context.Database.SqlQuery<ContractDetailViewModel>(query).ToList();

            var user = auth.getUser(Request);

            var res = auth.filerByRegional(rawresult.AsQueryable(), user);

            return Ok(res);
        }

        [HttpGet]
        [Route("api/ContractSAP")]
        public IHttpActionResult GetSAP()
        {
            DateTime date = DateTime.Now;
            var contplist = _context.ContractDetails
                .Include(p => p.Branches)
                .Include(p => p.Dependency)
                .Include(p => p.Positions)
                .Include(p => p.People).ToList()
                .Where(x => /*x.StartDate <= date
                            && */(x.EndDate == null || x.EndDate.Value.Year * 100 + x.EndDate.Value.Month >= date.Year * 100 + date.Month))
                .OrderByDescending(x => x.StartDate)
                .ToList()
                .Select(x => new
                {
                    x.People.CUNI,
                    x.People.Document,
                    FullName = x.People.GetFullName(),
                    Dependency = x.Dependency.Name,
                    DependencyCod = x.Dependency.Cod,
                    x.BranchesId
                }).ToList();
            var user = auth.getUser(Request);

            var res = auth.filerByRegional(contplist.AsQueryable(), user);

            return Ok(res);
        }

        [HttpGet]
        [Route("api/Contract/GetPersonHistory/{id}")]
        public IHttpActionResult GetHistory(int id,[FromUri] string all = "true")
        {
            if (all=="true")
            {
                var history = from contract in _context.ContractDetails.Include(x => x.Dependency)
                        .Include(x => x.Positions)
                        .Include(x => x.Link)
                        .Where(x => x.PeopleId == id).ToList()
                    join brs in _context.Branch on contract.Dependency.BranchesId equals brs.Id
                    select new
                    {
                        contract.Id,
                        contract.DependencyId,
                        contract.Dependency.Cod,
                        Dependency = contract.Dependency.Name,
                        contract.Dependency.BranchesId,
                        Branches = brs.Name,
                        Positions = contract.Positions.Name,
                        contract.PositionsId,
                        contract.PositionDescription,
                        contract.Dedication,
                        Linkage = contract.Link.Value,
                        contract.StartDate,
                        contract.EndDate
                    };
                return Ok(history);
            }
            else
            {
                DateTime date = DateTime.Now;
                var history = from contract in _context.ContractDetails.Include(x => x.Dependency)
                        .Include(x => x.Positions)
                        .Include(x => x.Link)
                        .Where(x => x.PeopleId == id).ToList()
                    join brs in _context.Branch on contract.Dependency.BranchesId equals brs.Id
                             // where (contract.EndDate == null || contract.EndDate.Value.Year * 100 + contract.EndDate.Value.Month >= date.Year * 100 + date.Month)
                    where (contract.Active == true )
                    select new
                    {
                        contract.Id,
                        contract.DependencyId,
                        contract.Dependency.Cod,
                        Dependency = contract.Dependency.Name,
                        contract.Dependency.BranchesId,
                        Branches = brs.Name,
                        Positions = contract.Positions.Name,
                        contract.PositionsId,
                        contract.PositionDescription,
                        contract.Dedication,
                        Linkage = contract.Link.Value,
                        contract.StartDate,
                        contract.EndDate
                    };
                return Ok(history);
            }   
        }

        // GET api/Contract
        [Route("api/Contract/{id}")]
        public IHttpActionResult GetContract(int id)
        {
            DateTime date = DateTime.Now;
            var contplist = _context.Database.SqlQuery<ContractDetailForWatch>(
                    "select\r\ncd.\"Id\",\r\ncd.\"CUNI\",\r\ncd.\"PeopleId\",\r\npe.\"Document\",\r\nf.\"FullName\",\r\ncd.\"DependencyId\",\r\nd.\"Name\" \"Dependency\",\r\nb.\"Abr\" \"Branches\",\r\ncd.\"BranchesId\",\r\ncd.\"PositionsId\",\r\np.\"Name\" \"Positions\",\r\ncd.\"PositionDescription\",\r\ncd.\"AI\",\r\ncd.\"Dedication\",\r\ntt.\"Value\" \"Linkagestr\",\r\ncd.\"Linkage\",\r\ncd.\"StartDate\",\r\ncd.\"EndDate\",\r\ncd.\"NumGestion\",\r\ncd.\"Comunicado\",\r\ncd.\"Respaldo\",\r\ncd.\"Seguimiento\",\r\ncd.\"EndDateNombramiento\",\r\ncase when vs.cuni is not null and vl.cuni is null then 'S'\r\nwhen vl.cuni is not null and vs.cuni is null then 'L'\r\nwhen vl.cuni is not null and vs.cuni is not null then 'LYS'\r\nend as \"ValidoPara\"\r\nfrom " +
                    CustomSchema.Schema + ".\"ContractDetail\" cd\r\ninner join " + CustomSchema.Schema +
                    ".\"People\" pe on pe.\"Id\" = cd.\"PeopleId\"\r\ninner join " + CustomSchema.Schema +
                    ".\"FullName\" f on f.\"PeopleId\" = cd.\"PeopleId\"\r\ninner join " + CustomSchema.Schema +
                    ".\"Dependency\" d on d.\"Id\" = cd.\"DependencyId\"\r\ninner join " + CustomSchema.Schema +
                    ".\"TableOfTables\" tt on tt.\"Id\" = cd.\"Linkage\"\r\ninner join " + CustomSchema.Schema +
                    ".\"Branches\" b on b.\"Id\" = cd.\"BranchesId\"\r\ninner join " + CustomSchema.Schema +
                    ".\"Position\" p on p.\"Id\" = cd.\"PositionsId\"\r\nleft join " + CustomSchema.Schema +
                    ".\"ValidoSalomon\" vs on vs.\"ContractDetailId\" = cd.\"Id\"\r\nleft join " + CustomSchema.Schema +
                    ".\"ValidoListados\" vl on vl.\"ContractDetailId\" = cd.\"Id\"\r\nwhere cd.\"Id\" =" + id)
                .AsQueryable();
                var sele = contplist
                .Select(x => new
                {
                    x.Id,
                    x.CUNI,
                    x.PeopleId,
                    x.Document,
                    x.FullName,
                    x.DependencyId,
                    x.Dependency,
                    x.Branches,
                    x.BranchesId,
                    x.PositionsId,
                    x.Positions,
                    x.PositionDescription,
                    x.AI,
                    x.Dedication,
                    x.Linkagestr,
                    x.Linkage,
                    StartDatestr = x.StartDate.ToString("dd MMM yyyy", new CultureInfo("es-ES")),
                    EndDatestr = x.EndDate == null ? "" : x.EndDate.GetValueOrDefault().ToString("dd MMM yyyy", new CultureInfo("es-ES")),
                    StartDate = x.StartDate.ToString("MM/dd/yyyy"),
                    EndDate = x.EndDate == null ? "" : x.EndDate.Value.ToString("MM/dd/yyyy"),
                    x.NumGestion,
                    x.Comunicado,
                    x.Respaldo,
                    x.Seguimiento,
                    x.EndDateNombramiento,
                    x.ValidoPara
                });

            var user = auth.getUser(Request);
            var res = auth.filerByRegional(sele.AsQueryable(), user);
            if (res.Count() == 0)
                return NotFound();

            return Ok(res.FirstOrDefault());
        }

        [HttpGet]
        [Route("api/Contract/GetPersonContract/{id}")]
        public IHttpActionResult GetPersonContract(int id)
        {
            List<ContractDetail> contractInDB = null;

            contractInDB = _context.ContractDetails.Where(d => d.People.Id == id).ToList();

            if (contractInDB == null)
                return NotFound();

            return Ok(contractInDB);
        }

        // todo convert this to a report in excel
        [HttpGet]
        [Route("api/Contract/GetContractsBranch/{id}")]
        public IHttpActionResult GetContractsBranch(int id)
        {
            List<ContractDetail> contractInDB = null;
            DateTime date=new DateTime(2018,9,1);
            DateTime date2=new DateTime(2019,1,1);
            var people = _context.ContractDetails.Include(x=>x.People).Include(x=>x.Branches).Include(x=>x.Link).Where(x=>  (x.EndDate==null || x.EndDate>date2)).Select(x=>x.People).Distinct();
            // var people = _context.CustomUsers.Include(x => x.People).Select(x => x.People);
            int i = people.Count();
            string res = "";
            var br = _context.Branch.ToList();
            var OU = _context.OrganizationalUnits.ToList();

            foreach (var person in people)
            {
                var contract = person.GetLastContract();
                var user = _context.CustomUsers.FirstOrDefault(x => x.PeopleId == contract.People.Id);
               /* res += contract.People.GetFullName() + ";";
                res += user.UserPrincipalName + ";";
                res += "NORMAL;";
                res += "NO;";
                res += contract.Branches.Abr + ";";
                res += "RENDICIONES;";
                res += contract.CUNI + ";";*/

                res += contract.People.CUNI + ";";
                res += contract.People.Document + ";";
                res += contract.People.GetFullName() + ";";
                res += contract.People.FirstSurName + ";";
                res += contract.People.SecondSurName + ";";
                res += contract.People.MariedSurName + ";";
                res += contract.People.Names + ";";
                res += contract.People.BirthDate + ";";
                res += contract.People.UcbEmail + ";";


                res += contract.Dependency.Cod + ";";
                res += contract.Dependency.Name + ";";

                var o = OU.FirstOrDefault(x => x.Id == contract.Dependency.OrganizationalUnitId);
                res += o.Cod + ";";
                res += o.Name + ";";

                res += contract.Positions.Name + ";";
                res += contract.Dedication + ";";
                res += contract.Link.Value + ";";
                res += contract.AI + ";";
                var lm = contract.People.GetLastManagerAuthorizator(_context);
                res += lm==null?";":lm.CUNI + ";";
                res += lm==null?";":lm.GetFullName() + ";";
                var lmlc = lm==null?null:lm.GetLastContract(_context);
                res += lmlc == null ? ";" : lmlc.Positions.Name + ";";
                res += lmlc == null ? ";" : lmlc.Dependency.Cod + ";";
                res += lmlc == null ? ";" : lmlc.Dependency.Name + ";";

                var b = br.FirstOrDefault(x => x.Id == contract.Dependency.BranchesId);
                res += b.Abr + ";";
                res += b.Name;

                res += "\n";
            }


            return Ok(res);
        }

        //altas
        // POST api/Contract/alta/4
        [HttpPost]
        [Route("api/Contract/Alta/{flag}")]
        public IHttpActionResult Post([FromBody]ContractDetail contract, string flag)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);
            People person = _context.Person.FirstOrDefault(x => x.Id == contract.PeopleId);

            if (person == null)
                person = _context.Person.FirstOrDefault(x => x.CUNI == contract.CUNI);

            if (person == null)
                return NotFound();

            bool valid = true;
            string errorMessage = "";
            if (contract.EndDate < contract.StartDate)
            {
                valid = false;
                errorMessage += "La fecha fin no puede ser menor a la fecha inicio";
            }

            if (_context.Dependencies.FirstOrDefault(x=>x.Id == contract.DependencyId ) == null)
            {
                valid = false;
                errorMessage += "La Dependencia no es valida";
            }

            if (_context.Position.FirstOrDefault(x => x.Id == contract.PositionsId) == null)
            {
                valid = false;
                errorMessage += "El Cargo no es valido";
            }

            if ( _context.TableOfTableses.FirstOrDefault(x => x.Type == "VINCULACION" && x.Id == contract.Linkage) == null)
            {
                valid = false;
                errorMessage += "Esta vinculación no es valida";
            }

            if (_context.TableOfTableses.FirstOrDefault(x => x.Type == "DEDICACION" && x.Value == contract.Dedication) == null)
            {
                valid = false;
                errorMessage += "Esta dedicación no es valida";
            }

            if (valid)
            {
                contract.PeopleId = person.Id;
                contract.CUNI = person.CUNI;
                contract.BranchesId = _context.Dependencies.FirstOrDefault(x => x.Id == contract.DependencyId).BranchesId;
                contract.Id = ContractDetail.GetNextId(_context);
                contract.PositionDescription = contract.PositionDescription!=null?contract.PositionDescription.ToUpper():null;
                contract.Active = true;
                _context.ContractDetails.Add(contract);
                _context.SaveChanges();
                if (flag.Equals("S"))
                {
                    ValidoSalomon sa = new ValidoSalomon();
                    sa.ContractDetailId = contract.Id;
                    sa.CUNI = contract.CUNI;
                    sa.PeopleId = _context.Person.FirstOrDefault(x => x.CUNI == sa.CUNI).Id;
                    _context.ValidoSalomons.Add(sa);
                    _context.SaveChanges();
                }
                else
                {
                    if (flag.Equals("L"))
                    {
                        ValidoListados vl = new ValidoListados();
                        vl.ContractDetailId = contract.Id;
                        vl.CUNI = contract.CUNI;
                        vl.PeopleId = _context.Person.FirstOrDefault(x => x.CUNI == vl.CUNI).Id;
                        _context.ValidoListadoses.Add(vl);
                        _context.SaveChanges();
                    }
                    else
                    {
                        ValidoSalomon sa = new ValidoSalomon();
                        ValidoListados vl = new ValidoListados();
                        sa.ContractDetailId = contract.Id;
                        sa.CUNI = contract.CUNI;
                        sa.PeopleId = _context.Person.FirstOrDefault(x => x.CUNI == sa.CUNI).Id;
                        vl.ContractDetailId = contract.Id;
                        vl.CUNI = contract.CUNI;
                        vl.PeopleId = _context.Person.FirstOrDefault(x => x.CUNI == vl.CUNI).Id;
                        _context.ValidoSalomons.Add(sa);
                        _context.ValidoListadoses.Add(vl);
                        _context.SaveChanges();
                    }
                }

                

                var user = auth.getUser(Request);

                //se obtiene el nombre de la posición del contrato que se está registrando en el controlador
                string contractPosition = _context.Position.FirstOrDefault(x => x.Id == contract.PositionsId).NameAbr;
                // create user in SAP
                B1.AddOrUpdatePerson(user.Id, person, contractPosition);

                
                return Created(new Uri(Request.RequestUri + "/" + contract.Id), contract);
            }

            return BadRequest(errorMessage);

        }

        //Bajas
        // POST api/Contract/Baja/5
        [HttpPost]
        [Route("api/Contract/Baja/{id}")]
        public IHttpActionResult Baja(int id, ContractDetail contract)
        {
            ContractDetail contractInDB = _context.ContractDetails.FirstOrDefault(d => d.Id == id);
            // contractInDB.EndDate=DateTime.Now;
            if (contract.EndDate <= contractInDB.StartDate)
            {
                return BadRequest("No es posible dar de baja el registro. La fecha final NO puede ser menor a la fecha inicio.");
            }
            else
            {
                ChangesLogs log = new ChangesLogs();
                log.AddChangesLog(contractInDB, contract, new List<string>() { "EndDate", "Cause" });
                contractInDB.EndDate = contract.EndDate;
                contractInDB.Cause = contract.Cause;
                contractInDB.Active = false;
                contractInDB.UpdatedAt = DateTime.Now;
                _context.SaveChanges();
                return Ok(contractInDB);
            }
            
        }
        //Bajas
        // GET api/Contract/BajaPendiente/
        [HttpGet]
        [Route("api/Contract/BajaPendiente")]
        public IHttpActionResult BajaPendiente()
        {
            var query = "select  cd.\"Id\", cd.\"CUNI\", c.\"NOMBRE COMPLETO\" \"FullName\", c.\"POSICION\" \"Positions\", c.\"DEPENDENCIA\" \"Dependency\", c.\"REGIONAL\" \"Branches\", cd.\"EndDate\", cd.\"StartDate\" " +
                        " from " + CustomSchema.Schema + ". \"ContractDetail\" cd " +
                        " inner join " + CustomSchema.Schema + ". \"CONTRATOS\" c on c.\"ID\" = cd.\"Id\" " +
                        " where cd.\"EndDate\" is not null and year(cd.\"EndDate\")*100+month(cd.\"EndDate\") <= year(current_date)*100+month(current_date) and cd.\"Active\" = true ";
            var rawresult = _context.Database.SqlQuery<ContractDetailViewModel>(query).ToList();

            var user = auth.getUser(Request);

            var res = auth.filerByRegional(rawresult.AsQueryable(), user);

            return Ok(res);
        }

        // GET api/Contract/BajaPendiente/
        [HttpPost]
        [Route("api/Contract/ConfirmBajaPendiente")]
        public IHttpActionResult ConfirmBajaPendiente(JObject data)
        {
            var list = data.Values().ToList()[0].ToList();
            foreach (var d in list)
            {
                var id = Int32.Parse(d.ToString());
                var contract = _context.ContractDetails.FirstOrDefault(x => x.Id == id);
                if (contract != null)
                {
                    contract.Cause = "5";
                    contract.Active = false;
                    contract.UpdatedAt = DateTime.Now;
                }
            }
            _context.SaveChanges();
            return Ok();
        }

        // PUT api/Contract/5
        [HttpPut]
        [Route("api/Contract/{id}/{flag}")]
        public IHttpActionResult Put(int id, string flag, ContractDetail contract)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            ContractDetail contractInDB = _context.ContractDetails.FirstOrDefault(d => d.Id == id);
            if (contractInDB == null)
                return NotFound();
            // log changes
            ChangesLogs log = new ChangesLogs();
            log.AddChangesLog(contractInDB, contract, new List<string>() { "EndDate", "StartDate", "Dedication",  "DependencyId", "PositionsId", "PositionDescription", "Linkage", "AI" });
            // todo view rol and permisions to update or not
            contractInDB.StartDate = contract.StartDate;
            contractInDB.EndDate = contract.EndDate;
            contractInDB.Dedication = contract.Dedication;
            contractInDB.BranchesId = _context.Dependencies.FirstOrDefault(x=>x.Id==contract.DependencyId).BranchesId;
            contractInDB.DependencyId = contract.DependencyId;
            contractInDB.PositionsId = contract.PositionsId;
            contractInDB.PositionDescription = contract.PositionDescription == null ? null : contract.PositionDescription.ToUpper();
            contractInDB.Linkage = contract.Linkage;
            contractInDB.AI = contract.AI;
            contractInDB.NumGestion = contract.NumGestion;
            contractInDB.Seguimiento = contract.Seguimiento;
            contractInDB.Respaldo = contract.Respaldo;
            contractInDB.Comunicado = contract.Comunicado;
            contractInDB.UpdatedAt = DateTime.Now;
            contractInDB.EndDateNombramiento = contract.EndDateNombramiento;

            var S = _context.ValidoSalomons.FirstOrDefault(x => x.ContractDetailId == id);
            var L = _context.ValidoListadoses.FirstOrDefault(x => x.ContractDetailId == id);

            if (S != null && L != null)
            {
                if (flag.Equals("S"))
                {
                    _context.ValidoListadoses.Remove(L);
                }
                else
                {
                    if (flag.Equals("L"))
                    {
                        _context.ValidoSalomons.Remove(S);
                    }
                }
            }
            else
            {
                if (S != null && L == null)
                {
                    if (flag.Equals("L"))
                    {
                        _context.ValidoSalomons.Remove(S);
                        ValidoListados LNew = new ValidoListados();
                        LNew.CUNI = contract.CUNI;
                        LNew.ContractDetailId = contract.Id;
                        LNew.PeopleId = contract.PeopleId;
                        LNew.Prioridad = 1;
                        _context.ValidoListadoses.Add(LNew);
                    }
                    else
                    {
                        if (flag.Equals("LYS"))
                        {
                            ValidoListados LNew = new ValidoListados();
                            LNew.CUNI = contract.CUNI;
                            LNew.ContractDetailId = contract.Id;
                            LNew.PeopleId = contract.PeopleId;
                            LNew.Prioridad = 1;
                            _context.ValidoListadoses.Add(LNew);
                        }
                    }
                }
                else
                {
                    if (S == null && L != null)
                    {
                        if (flag.Equals("S"))
                        {
                            _context.ValidoListadoses.Remove(L);
                            ValidoSalomon SNew = new ValidoSalomon();
                            SNew.CUNI = contract.CUNI;
                            SNew.ContractDetailId = contract.Id;
                            SNew.PeopleId = contract.PeopleId;
                            _context.ValidoSalomons.Add(SNew);
                        }
                        else
                        {
                            if (flag.Equals("LYS"))
                            {
                                ValidoSalomon SNew = new ValidoSalomon();
                            SNew.CUNI = contract.CUNI;
                            SNew.ContractDetailId = contract.Id;
                            SNew.PeopleId = contract.PeopleId;
                            _context.ValidoSalomons.Add(SNew);
                            }
                        }
                    }
                }
            }


            var person = _context.Person.FirstOrDefault(x => x.CUNI == contractInDB.CUNI);

            var user = auth.getUser(Request);

            //se obtiene el nombre de la posición del contrato que se está registrando en el controlador
            string contractPosition = _context.Position.FirstOrDefault(x => x.Id == contract.PositionsId).NameAbr;
            // create user in SAP
            B1.AddOrUpdatePerson(user.Id, person, contractPosition);

            _context.SaveChanges();
            return Ok(contractInDB);
        }

        // DELETE api/Contract/5
        [HttpDelete]
        public IHttpActionResult ContractDelete(int id)
        {
            var contractInDB = _context.Contracts.FirstOrDefault(d => d.Id == id);
            if (contractInDB == null)
                return NotFound();
            _context.Contracts.Remove(contractInDB);
            _context.SaveChanges();
            return Ok();
        }
        [HttpGet]
        [Route("api/SudoGetContract/{id}")]
        public IHttpActionResult SudoGetContract(int id)
        {
            var contplist = _context.Database.SqlQuery<SudoContractDetailViewModel>("select cd.*, fn.\"FullName\", b.\"Name\" \"Branches\", p.\"Document\" " +
                                                                                    " from " + CustomSchema.Schema + ".\"ContractDetail\" cd " +
                                                                                    " inner join " + CustomSchema.Schema + ".\"FullName\" fn on fn.\"CUNI\" = cd.cuni " +
                                                                                    " inner join " + CustomSchema.Schema + ".\"People\" p on p.\"CUNI\" = cd.cuni " +
                                                                                    " inner join " + CustomSchema.Schema + ".\"Branches\" b on b.\"Id\" = cd.\"BranchesId\" " +
                                                                                    " where cd.\"Id\" =" + id).ToList();


            if (contplist.Count() == 0)
                return NotFound();

            return Ok(contplist.FirstOrDefault());
        }
        [HttpPut]
        [Route("api/SudoUpdateContract/{id}")]
        public IHttpActionResult SudoUpdateContract(int id, ContractDetail contract)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            ContractDetail contractInDB = _context.ContractDetails.FirstOrDefault(d => d.Id == id);
            if (contractInDB == null)
                return NotFound();
            // log changes
            ChangesLogs log = new ChangesLogs();
            log.AddChangesLog(contractInDB, contract, new List<string>() { "EndDate", "StartDate", "Dedication", "DependencyId", "PositionsId", "PositionDescription", "Linkage", "AI" });
            // todo view rol and permisions to update or not
            contractInDB.StartDate = contract.StartDate;
            contractInDB.EndDate = contract.EndDate;
            contractInDB.Dedication = contract.Dedication;
            contractInDB.BranchesId = _context.Dependencies.FirstOrDefault(x => x.Id == contract.DependencyId).BranchesId;
            contractInDB.DependencyId = contract.DependencyId;
            contractInDB.PositionsId = contract.PositionsId;
            contractInDB.PositionDescription = contract.PositionDescription == null ? null : contract.PositionDescription.ToUpper();
            contractInDB.Linkage = contract.Linkage;
            contractInDB.AI = contract.AI;
            contractInDB.NumGestion = contract.NumGestion;
            contractInDB.Seguimiento = contract.Seguimiento;
            contractInDB.Respaldo = contract.Respaldo;
            contractInDB.Comunicado = contract.Comunicado;
            contractInDB.UpdatedAt = DateTime.Now;
            contractInDB.EndDateNombramiento = contract.EndDateNombramiento;
            contractInDB.Active = contract.Active;
            contractInDB.Cause = contract.Cause;
            var person = _context.Person.FirstOrDefault(x => x.CUNI == contractInDB.CUNI);

            var user = auth.getUser(Request);

            //se obtiene el nombre de la posición del contrato que se está registrando en el controlador
            string contractPosition = _context.Position.FirstOrDefault(x => x.Id == contract.PositionsId).NameAbr;
            // create user in SAP
            B1.AddOrUpdatePerson(user.Id, person, contractPosition);

            _context.SaveChanges();
            return Ok(contractInDB);
        }

        [HttpGet]
        [Route("api/CheckFechaIngreso/{cuni}")]
        public string CheckFechaIngreso(string cuni)
        {
            var exist = _context.Database.SqlQuery<SudoContractDetailViewModel>("select cd.*" +
                                                                                    " from " + CustomSchema.Schema +
                                                                                    ".\"Antiguedad\" cd " +
                                                                                    " where cd.\"CUNI\" = '" + cuni +
                                                                                    "'").FirstOrDefault();

            string res = "Correcto";

            if (exist == null)
            {
                var fecha = _context.Database.SqlQuery<SudoContractDetailViewModel>("select cd.*" +
                                                                                    " from " + CustomSchema.Schema +
                                                                                    ".\"ContractDetail\" cd " +
                                                                                    " where cd.\"CUNI\" = '" + cuni +
                                                                                    "'").FirstOrDefault();
                Antiguedad ant = new Antiguedad();

                ant.CUNI = fecha.CUNI;
                ant.ContractDetailId = fecha.Id;
                ant.Activo = true;
                _context.Antiguedades.Add(ant);
                _context.SaveChanges();
                res = "Se guardo esta fecha de inicio registrada como fecha de ingreso.";
            }
            else
            {
                var fecha = _context.Database.SqlQuery<SudoContractDetailViewModel>("select cd.* from " + CustomSchema.Schema + ".\"ContractDetail\" cd " +
                                                                                    " inner join " + CustomSchema.Schema + ".\"Antiguedad\" a on a.\"ContractDetailId\" = cd.\"Id\"   " +
                                                                                    " where a.\"CUNI\" = '" + cuni +
                                                                                    "'").FirstOrDefault();
                res = "Se tiene una fecha de ingreso registrada: " + fecha.StartDate.Day + "-" + fecha.StartDate.Month + "-" + fecha.StartDate.Year + ". Si desea cambiarla ingrese a la pestaña \"Gestion Fechas de Ingreso\".";
            }

            return res;
        }

        [HttpGet]
        [Route("api/getCUNI/{PeopleId}")]
        public string getCUNI(string PeopleId)
        {
            var exist = _context.Database.SqlQuery<SudoContractDetailViewModel>("select cuni" +
                                                                                    " from " + CustomSchema.Schema +
                                                                                    ".\"People\" " +
                                                                                    " where \"Id\" = " + PeopleId ).FirstOrDefault();
            
            return exist.CUNI;
        }

        [HttpGet]
        [Route("api/getSpecialCases/{PeopleId}")]
        public string getSpecialCases(string PeopleId)
        {
            var exist = _context.Database.SqlQuery<AuxiliarBranches>("select * from " +
                                                                     "(\r\nselect \r\ncase when vl.\"ContractDetailId\" " +
                                                                     "is not null and vs.\"ContractDetailId\" is not null then 'LYS'\r\nwhen vl.\"ContractDetailId\" " +
                                                                     "is null and vs.\"ContractDetailId\" is not null then 'S'\r\nwhen vl.\"ContractDetailId\" " +
                                                                     "is not null and vs.\"ContractDetailId\" is null then 'L'\r\nend as \"ValidoPara\"" +
                                                                     "\r\nfrom " + CustomSchema.Schema + ".\"ContractDetail\" cd" +
                                                                     "\r\nleft join " + CustomSchema.Schema + ".\"ValidoListados\" vl on vl.\"ContractDetailId\" = cd.\"Id\"" +
                                                                     "\r\nleft join " + CustomSchema.Schema + ".\"ValidoSalomon\" vs on vs.\"ContractDetailId\" = cd.\"Id\"" +
                                                                     "\r\nwhere cd.\"PeopleId\" = " + PeopleId +"\r\n) x  group by x.\"ValidoPara\"").ToList();
            string aux = "NO";
            if (exist.Count > 1)
            {
                aux = "SI";
            }

            return aux;
        }
        //todo falta hacer los listados por solo salomon o solo listado
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/people/GetContractsListado/{id}")]
        public IHttpActionResult GetContractsListado(int id)
        {

            var contplist = _context.Database.SqlQuery<ContractDetailListadoSalomon>(
                    "select cd.\"Id\", cd.\"CUNI\", cd.\"PeopleId\", pe.\"Document\", f.\"FullName\", d.\"Cod\", d.\"Name\" \"Dependency\", concat(ou.\"Cod\", " +
                    "concat ('-', ou.\"Name\")) \"OU\",\r\nb.\"Abr\" \"Branches\", cd.\"BranchesId\", cd.\"PositionsId\", p.\"Name\" \"Positions\", cd.\"PositionDescription\", " +
                    "cd.\"AI\", cd.\"Dedication\",\r\ntt.\"Value\" \"Link\", cd.\"StartDate\", cd.\"EndDate\", cd.\"NumGestion\", cd.\"Comunicado\", cd.\"Respaldo\",\r\ncd.\"Seguimiento\", " +
                    "cd.\"EndDateNombramiento\"" +
                    "\r\nfrom  " + CustomSchema.Schema + ".\"ContractDetail\" cd" +
                    "\r\ninner join  " + CustomSchema.Schema + ".\"People\" pe on pe.\"Id\" = cd.\"PeopleId\"" +
                    "\r\ninner join  " + CustomSchema.Schema + ".\"FullName\" f on f.\"PeopleId\" = cd.\"PeopleId\" " +
                    "\r\ninner join  " + CustomSchema.Schema + ".\"Dependency\" d on d.\"Id\" = cd.\"DependencyId\"" +
                    "\r\ninner join  " + CustomSchema.Schema + ".\"OrganizationalUnit\" ou on d.\"OrganizationalUnitId\" = ou.\"Id\"" +
                    "\r\ninner join  " + CustomSchema.Schema + ".\"TableOfTables\" tt on tt.\"Id\" = cd.\"Linkage\"" +
                    "\r\ninner join  " + CustomSchema.Schema + ".\"Branches\" b on b.\"Id\" = cd.\"BranchesId\"" +
                    "\r\ninner join  " + CustomSchema.Schema + ".\"Position\" p on p.\"Id\" = cd.\"PositionsId\"" +
                    "\r\ninner join  " + CustomSchema.Schema + ".\"ValidoListados\" vl on vl.\"ContractDetailId\" = cd.\"Id\"" +
                    "\r\nwhere cd.\"PeopleId\" =" + id + " order by cd.\"Active\" desc,cd.\"EndDate\" desc, cd.\"StartDate\" desc")
                .AsQueryable();
            var sele = contplist
            .Select(x => new
            {
                x.Id,
                x.CUNI,
                x.PeopleId,
                x.Document,
                x.FullName,
                x.Cod,
                x.Dependency,
                x.OU,
                x.Branches,
                x.BranchesId,
                x.PositionsId,
                x.Positions,
                x.PositionDescription,
                x.AI,
                x.Dedication,
                x.Link,
                StartDatestr = x.StartDate.ToString("dd MMM yyyy", new CultureInfo("es-ES")),
                EndDatestr = x.EndDate == null ? "" : x.EndDate.GetValueOrDefault().ToString("dd MMM yyyy", new CultureInfo("es-ES")),
                StartDate = x.StartDate.ToString("MM/dd/yyyy"),
                EndDate = x.EndDate == null ? "" : x.EndDate.Value.ToString("MM/dd/yyyy"),
                x.NumGestion,
                x.Comunicado,
                x.Respaldo,
                x.Seguimiento,
                x.EndDateNombramiento,
                x.ValidoPara
            });

            var user = auth.getUser(Request);
            var res = auth.filerByRegional(sele.AsQueryable(), user);
            

            return Ok(res.ToList());
        }
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/people/GetContractsSalomon/{id}")]
        public IHttpActionResult GetContractsSalomon(int id)
        {

            var contplist = _context.Database.SqlQuery<ContractDetailListadoSalomon>(
                   "select cd.\"Id\", cd.\"CUNI\", cd.\"PeopleId\", pe.\"Document\", f.\"FullName\", d.\"Cod\", d.\"Name\" \"Dependency\", concat(ou.\"Cod\", " +
                   "concat ('-', ou.\"Name\")) \"OU\",\r\nb.\"Abr\" \"Branches\", cd.\"BranchesId\", cd.\"PositionsId\", p.\"Name\" \"Positions\", cd.\"PositionDescription\", " +
                   "cd.\"AI\", cd.\"Dedication\",\r\ntt.\"Value\" \"Link\", cd.\"StartDate\", cd.\"EndDate\", cd.\"NumGestion\", cd.\"Comunicado\", cd.\"Respaldo\",\r\ncd.\"Seguimiento\", " +
                   "cd.\"EndDateNombramiento\"" +
                   "\r\nfrom  " + CustomSchema.Schema + ".\"ContractDetail\" cd" +
                   "\r\ninner join  " + CustomSchema.Schema + ".\"People\" pe on pe.\"Id\" = cd.\"PeopleId\"" +
                   "\r\ninner join  " + CustomSchema.Schema + ".\"FullName\" f on f.\"PeopleId\" = cd.\"PeopleId\" " +
                   "\r\ninner join  " + CustomSchema.Schema + ".\"Dependency\" d on d.\"Id\" = cd.\"DependencyId\"" +
                   "\r\ninner join  " + CustomSchema.Schema + ".\"OrganizationalUnit\" ou on d.\"OrganizationalUnitId\" = ou.\"Id\"" +
                   "\r\ninner join  " + CustomSchema.Schema + ".\"TableOfTables\" tt on tt.\"Id\" = cd.\"Linkage\"" +
                   "\r\ninner join  " + CustomSchema.Schema + ".\"Branches\" b on b.\"Id\" = cd.\"BranchesId\"" +
                   "\r\ninner join  " + CustomSchema.Schema + ".\"Position\" p on p.\"Id\" = cd.\"PositionsId\"" +
                   "\r\ninner join  " + CustomSchema.Schema + ".\"ValidoSalomon\" vl on vl.\"ContractDetailId\" = cd.\"Id\"" +
                   "\r\nwhere cd.\"PeopleId\" =" + id + " order by cd.\"Active\" desc,cd.\"EndDate\" desc, cd.\"StartDate\" desc")
               .AsQueryable();
            var sele = contplist
            .Select(x => new
            {
                x.Id,
                x.CUNI,
                x.PeopleId,
                x.Document,
                x.FullName,
                x.Cod,
                x.Dependency,
                x.OU,
                x.Branches,
                x.BranchesId,
                x.PositionsId,
                x.Positions,
                x.PositionDescription,
                x.AI,
                x.Dedication,
                x.Link,
                StartDatestr = x.StartDate.ToString("dd MMM yyyy", new CultureInfo("es-ES")),
                EndDatestr = x.EndDate == null ? "" : x.EndDate.GetValueOrDefault().ToString("dd MMM yyyy", new CultureInfo("es-ES")),
                StartDate = x.StartDate.ToString("MM/dd/yyyy"),
                EndDate = x.EndDate == null ? "" : x.EndDate.Value.ToString("MM/dd/yyyy"),
                x.NumGestion,
                x.Comunicado,
                x.Respaldo,
                x.Seguimiento,
                x.EndDateNombramiento,
                x.ValidoPara
            });

            var user = auth.getUser(Request);
            var res = auth.filerByRegional(sele.AsQueryable(), user);

            return Ok(res.ToList());
        }
    }
}
