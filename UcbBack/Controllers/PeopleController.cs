using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.UI.WebControls;
using Newtonsoft.Json.Linq;
using UcbBack.Logic;
using UcbBack.Models;
using System.Data.Entity;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Ajax.Utilities;
using UcbBack.Logic.B1;
using UcbBack.Logic.Mail;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;
using UcbBack.Models.Not_Mapped.ViewMoldes;
using System.Configuration;


namespace UcbBack.Controllers
{
    public class PeopleController : ApiController
    {
        private ApplicationDbContext _context;
        private ValidatePerson validator;
        private ValidateAuth auth;
        private ADClass activeDirectory;
        private B1Connection B1;


        public PeopleController()
        {
            _context = new ApplicationDbContext();
            validator = new ValidatePerson(_context);
            auth = new ValidateAuth();
            activeDirectory = new ADClass();
            B1=B1Connection.Instance();
        }

        // GET api/People
        public IHttpActionResult Get()
        {
            string query = "select p.\"Id\",p.CUNI,p.\"Document\",lc.\"FullName\", case when (lc.\"Active\"=false and lc.\"EndDate\"<current_date) then 'Inactivo' else 'Activo' end as \"Status\", lc.\"BranchesId\"  " +
                           "from " + CustomSchema.Schema + ".\"People\" p " +
                           "inner join " + CustomSchema.Schema + ".\"LASTCONTRACTS\" lc " +
                           "on p.cuni = lc.cuni " +
                           " order by \"FullName\";";
            var rawResult = _context.Database.SqlQuery<ContractDetailViewModel>(query).Select(x=>new
            {
                x.Id,
                x.CUNI,
                x.Document,
                x.FullName,
                x.Status,
                x.BranchesId
            }).AsQueryable();

            var user = auth.getUser(Request);

            var result = auth.filerByRegional(rawResult, user).ToList().Select(x=>new
            {
                x.Id,
                x.CUNI,
                x.Document,
                x.FullName,
                x.Status
            }).ToList();

            return Ok(result);
        }

        
        // GET api/People/5
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/people/Query")]
        public IHttpActionResult Query([FromUri] string by, [FromUri] string value)
        {
            //todo cleanText of "Value"

            People person = null;
            switch (by)
            {
                case "CUNI":
                    person = _context.Person.FirstOrDefault(x=>x.CUNI==value);
                    break;
                case "Documento":
                    string no_start_zeros = value.Replace("E-","").TrimStart('0');
                    person = _context.Person.ToList().FirstOrDefault(x => x.Document.Replace("E-", "").TrimStart('0') == value.Replace("E-", "").TrimStart('0'));
                    break;
                case "FullName":
                    person = _context.Person.ToList().FirstOrDefault(x => x.GetFullName() == value);
                    break;
                default:
                    return BadRequest();
            }

            if (person == null)
                return NotFound();

            dynamic res = new JObject();
            res.Id = person.Id;
            res.CUNI = person.CUNI;
            res.Document = person.Document;
            res.FullName = person.GetFullName();
            res.contract = person.GetLastContract(_context, DateTime.Now) != null;

            return Ok(res);
        }

        [System.Web.Http.NonAction]
        public async Task<System.Dynamic.ExpandoObject> HttpContentToVariables(MultipartMemoryStreamProvider req)
        {
            dynamic res = new System.Dynamic.ExpandoObject();
            foreach (HttpContent contentPart in req.Contents)
            {
                var contentDisposition = contentPart.Headers.ContentDisposition;
                string varname = contentDisposition.Name;

                if (varname == "\"file\"")
                {
                    Stream stream = await contentPart.ReadAsStreamAsync();
                    res.fileName = String.IsNullOrEmpty(contentDisposition.FileName) ? "" : contentDisposition.FileName.Trim('"');
                    res.excelStream = stream;
                }
            }
            return res;
        }

        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/people/UploadCI/{id}")]
        public Task<HttpResponseMessage> getCI(int id)
        {
            HttpRequestMessage httpRequestMessage = Request;
            HttpResponseMessage httpResponseMessage = httpRequestMessage.CreateResponse(HttpStatusCode.NotFound);

            var person = _context.Person.FirstOrDefault(x => x.Id == id);
            if (person == null)
                return System.Threading.Tasks.Task.FromResult(httpResponseMessage);
            if (person.DocPath == null)
                return System.Threading.Tasks.Task.FromResult(httpResponseMessage);
            var dataBytes = File.ReadAllBytes(person.DocPath);
            var CIStuff = new MemoryStream(dataBytes);  

            httpRequestMessage = Request;
            httpResponseMessage = httpRequestMessage.CreateResponse(HttpStatusCode.OK);
            httpResponseMessage.Content = new StreamContent(CIStuff);
            //httpResponseMessage.Content = new ByteArrayContent(bookStuff.ToArray());  
            httpResponseMessage.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment");
            var filename = person.DocPath.Split('\\');
            httpResponseMessage.Content.Headers.ContentDisposition.FileName = filename[filename.Length - 1];
            httpResponseMessage.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

            return System.Threading.Tasks.Task.FromResult(httpResponseMessage);

        }

        [System.Web.Http.HttpPost]
        [System.Web.Http.Route("api/people/UploadCI/{id}")]
        public HttpResponseMessage UploadCI(int id)
        {
            if (!Request.Content.IsMimeMultipartContent("form-data"))
                return Request.CreateResponse(HttpStatusCode.InternalServerError);
            var request = HttpContext.Current.Request;
            bool SubmittedFile = (request.Files.AllKeys.Length > 0);

            var response = new HttpResponseMessage();
            var file = request.Files[0];

            var person = _context.Person.FirstOrDefault(x => x.Id == id);
            if (person == null)
            {
                response.StatusCode = HttpStatusCode.NotFound;
                return response;
            }

            if (SubmittedFile)
            {
                try
                {
                    var type = file.ContentType.ToLower();
                    if (type != "image/jpg" &&
                        type != "image/jpeg" &&
                        type != "image/pjpeg" &&
                        type != "image/gif" &&
                        type != "image/x-png" &&
                        type != "application/pdf" &&
                        type != "image/png")
                    {
                        response.StatusCode = HttpStatusCode.BadRequest;
                        response.Content = new StringContent("Invalid File: " + type);
                        return response;
                    }
                    string path = Path.Combine(HttpContext.Current.Server.MapPath("~/Images/PeopleDocuments"),
                        (person.CUNI + Path.GetExtension(file.FileName)));
                    file.SaveAs(path);
                    person.DocPath = path;
                    _context.SaveChanges();
                    response.StatusCode = HttpStatusCode.OK;
                    response.Content = new StringContent(path);
                    return response;

                }
                catch (Exception ex)
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    response.Content = new StringContent("{\"Message\": \"Invalid File" + ex.Message + "\"}");
                    return response;
                }
            }
            else
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                response.Content = new StringContent("No File");
                return response;
            }
        }

        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/people/Contracts/{id}")]
        public IHttpActionResult GetContracts(int id, [FromUri] string now = "NO")
        {
            var positions = _context.Position
                .Include(x => x.Id)
                .Include(x => x.LevelId)
                .Include(x => x.Level);
            var contracts = _context.ContractDetails
                .Include(x => x.Dependency)
                .Include(x => x.Positions)
                .Include(x => x.Link)
                .Include(x => x.Branches)
                .Include(y => y.Dependency.OrganizationalUnit)
                .Where(x => x.PeopleId == id)
                .ToList()
                .Where(x=> now == "NO" || (x.EndDate == null || x.EndDate.Value > DateTime.Now))
                .OrderByDescending(x => x.Active)
                .ThenByDescending(x => x.StartDate)
                .Select(x => new
                {
                    x.Id,
                    x.Dependency.Cod,
                    Dependency = x.Dependency.Name,
                    OU = x.Dependency.OrganizationalUnit.Cod + " - " + x.Dependency.OrganizationalUnit.Name,
                    Link = x.Link.Value,
                    Branches = x.Branches.Name,
                    Positions = x.Positions.Name,
                    x.Dedication,
                    StartDatestr = x.StartDate.ToString("dd MMM yyyy", new CultureInfo("es-ES")),
                    EndDatestr = x.EndDate == null ? null : x.EndDate.Value.ToString("dd MMM yyyy", new CultureInfo("es-ES")),
                    StartDate = x.StartDate.ToString("MM-dd-yyyy"),
                    EndDate = x.EndDate == null ? null : x.EndDate.Value.ToString("MM-dd-yyyy"),
                    x.PositionDescription
                });
            //.OrderByDescending(x => x.EndDate == null ? DateTime.MaxValue : DateTime.MinValue)
            return Ok(contracts);
        }

        // GET api/People/5
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/people/{id}")]
        public IHttpActionResult Get(int id, [FromUri] string by = "Id")
        {

            People personInDB = null;
            switch (by)
            {
                case "Id":
                    personInDB = _context.Person.FirstOrDefault(d => d.Id == id);
                    break;
                case "Contract":
                    var con = _context.ContractDetails.Include(x=>x.People).FirstOrDefault(d => d.Id == id);
                    personInDB = con == null ? null : con.People;
                    break;
            }


            if (personInDB == null)
                return NotFound();
            var ADauth = new ADClass();
            var usr = auth.getUser(Request);
            var rols = ADauth.getUserRols(usr);
            var canUpdatePending = false;
            foreach (var rol in rols)
            {
                if (rol.Name == "GPS Admin" || rol.Name == "Admin" )
                {
                    canUpdatePending = true;
                    break;
                }
            }
            dynamic res = new JObject();
            res.Id = personInDB.Id;
            res.CUNI = personInDB.CUNI;
            res.Document = personInDB.Document;
            res.TypeDocument = personInDB.TypeDocument;
            res.Ext = personInDB.Ext;
            res.FullName = personInDB.GetFullName();
            res.FirstSurName = personInDB.FirstSurName;
            res.SecondSurName = personInDB.SecondSurName;
            res.Names = personInDB.Names;
            res.MariedSurName = personInDB.MariedSurName == null ? "" : personInDB.MariedSurName;
            res.UseMariedSurName = personInDB.UseMariedSurName;
            res.UseSecondSurName = personInDB.UseSecondSurName;
            res.Pending = personInDB.Pending;
            res.canUpdatePending = personInDB.Pending && canUpdatePending;
            var c = personInDB.GetLastContract(_context, date:DateTime.Now);
            res.Contract = c != null;
            res.ContractId = c == null ? (dynamic) "" : c.Id;
            res.PositionsId = c == null ? (dynamic) "" : c.Positions.Id;
            res.Positions = c == null ? "" : c.Positions.Name;
            res.PositionDescription = c == null ? "" : c.PositionDescription;
            res.AI = c == null ? false : c.AI;
            res.Dedication = c == null ? "" : c.Dedication;
            res.Linkage = c == null ? "" : c.Link.Value;
            res.DependencyId = c == null ? (dynamic) "" : c.Dependency.Id;
            res.Dependency = c == null ? "" : c.Dependency.Name;
            res.Branches = c == null ? null : _context.Branch.FirstOrDefault(x => x.Id == c.Dependency.BranchesId).Name;
            res.StartDatestr = c == null ? (dynamic) "" : c.StartDate.ToString("dd MMM yyyy", new CultureInfo("es-ES"));
            res.EndDatestr = c == null ? (dynamic)"" : c.EndDate == null ? "" : c.EndDate.Value.ToString("dd MMM yyyy", new CultureInfo("es-ES"));
            res.StartDate = c == null ? (dynamic)"" : c.StartDate.ToString("MM/dd/yyyy");
            res.EndDate = c == null ? (dynamic)"" : c.EndDate == null ? "" : c.EndDate.Value.ToString("MM/dd/yyyy");
            res.Gender = personInDB.Gender;
            res.BirthDatestr = personInDB.BirthDate.ToString("dd MMM yyyy", new CultureInfo("es-ES"));
            res.BirthDate = personInDB.BirthDate.ToString("MM/dd/yyyy");
            res.Nationality = personInDB.Nationality;
            res.AFP = personInDB.AFP;
            res.NUA = personInDB.NUA;
            res.Insurance = personInDB.Insurance;
            res.UcbEmail = personInDB.UcbEmail;
            res.PhoneNumber = personInDB.PhoneNumber;
            res.Categoria = personInDB.Categoria;
            res.PersonalEmail = personInDB.PersonalEmail;
            res.Age = DateTime.Now.Year - personInDB.BirthDate.Year;
            var u = _context.CustomUsers.FirstOrDefault(x => x.PeopleId == personInDB.Id);
            res.UserName = u == null ? "" : u.UserPrincipalName; return Ok(res);
        }

        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/people/manager/{id}")]
        public IHttpActionResult manager(int id)
        {
            People personInDB = _context.Person.FirstOrDefault(d => d.Id == id);
            if (personInDB == null)
                return NotFound();
            return Ok(personInDB.GetLastManagerAuthorizator());
        }


        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/addalltoSAPNow")]
        public IHttpActionResult addalltoSAPNow()
        {
            //actualiza a todas las personas de la vida para mostrar sus puestos, si no tienen la descripción en SAP
            var posSubject = _context.Database.SqlQuery<PositionSubjectViewModel>("select lc.\"PeopleId\" as \"People_Id\",p.\"NameAbr\" as \"NameAbr\" " + 
                                                                            "from " +ConfigurationManager.AppSettings["B1CompanyDB"]+".\"OHEM\" o "+
                                                                            "inner join " +CustomSchema.Schema+ ".\"LASTCONTRACTS\" lc "+
                                                                            "on o.\"ExtEmpNo\"=lc.cuni "+
                                                                            "inner join "+CustomSchema.Schema+".\"Position\" p " +
                                                                            "on p.\"Id\"=lc.\"PositionsId\" "+
                                                                            "where o.\"jobTitle\" is null ;").ToList();
            var usr = auth.getUser(Request);
            foreach (var p in posSubject)
            {
                var thisPerson = _context.Person.FirstOrDefault(x => x.Id == p.People_Id);
                var result=B1.AddOrUpdatePerson(usr.Id, thisPerson, p.NameAbr);
                p.Result = result;
            }

            return Ok( posSubject );
        }

        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/enableAllInAD/")]
        public IHttpActionResult enableAllInAD()
        {
            DateTime date = new DateTime(2018, 1, 1);
            List<People> person = _context.ContractDetails.Include(x => x.People).Include(x=>x.Positions).
                Where(y => (y.EndDate > date || y.EndDate == null)
                ).Select(x => x.People).Distinct().ToList();
            int i = 0;
            foreach (var pe in person)
            {
                i++;
                activeDirectory.enableUser(pe);
            }

            return Ok();
        }

        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/sendMail/")]
        public IHttpActionResult sendMail()
        {
            return Ok(Mail.SendMail());
        }

        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/addalltoAD/{cuni}")]
        public IHttpActionResult addalltoADId(string cuni)
        {

            List<string> palabras = new List<string>(new string[]
            {
                "aula",
                "libro",
                "lapiz",
                "papel",
                "folder",
                "lentes"
            });

            /*var usr = _context.CustomUsers.Select(x => x.PeopleId).ToList();*/
            People person = _context.ContractDetails.Include(x => x.People).Include(x => x.Positions).
                Where(y => y.CUNI == cuni
                    ).Select(x => x.People).Distinct().ToList().FirstOrDefault();

            Random rnd = new Random();

            //var tt = activeDirectory.findUser(pe);
            string pass = palabras[rnd.Next(6)];
            while (pass.Length < 8)
            {
                pass += rnd.Next(10);
            }

            var ex = _context.CustomUsers.FirstOrDefault(x => x.PeopleId == person.Id);
            if (ex == null)
            {
                activeDirectory.adddOrUpdate(person, pass);
                _context.SaveChanges();
                var account = _context.CustomUsers.FirstOrDefault(x => x.PeopleId == person.Id);
                account.AutoGenPass = pass;
                _context.SaveChanges();
            }

            var per = _context.Person.FirstOrDefault(x => x.CUNI == "AEC801205");
            var r = activeDirectory.findUser(per);
            return Ok(r);
        }

        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/addalltoAD")]
        public IHttpActionResult addalltoAD()
        {

            /*List<string> branches = _context.Branch.Select(x => x.ADGroupName).ToList();
            List<string> roles = _context.Rols.Select(x => x.ADGroupName).ToList();

            foreach (var branch in branches)
            {
                activeDirectory.createGroup(branch);
            }

            foreach (var rol in roles)
            {
                activeDirectory.createGroup(rol);
            }*/

            List<string> palabras = new List<string>(new string []
            {
                "aula",
                "libro",
                "lapiz",
                "papel",
                "folder",
                "lentes"
            });

        DateTime date = new DateTime(2018,1,1);
            //todo run for all people   solo fata pasar fechas de corte
            /*var usr = _context.CustomUsers.Select(x => x.PeopleId).ToList();*/
            List<People> person = _context.ContractDetails.Include(x => x.People).Include(x=>x.Positions).
                Where(y => (y.EndDate > date || y.EndDate == null) && y.CUNI == "SAG930730"
                    ).Select(x => x.People).Distinct().ToList();

            Random rnd = new Random();
            foreach (var pe in person)
            {
                //var tt = activeDirectory.findUser(pe);
                string pass = palabras[rnd.Next(6)];
                while (pass.Length<8)
                {
                    pass += rnd.Next(10);
                }

                var ex = _context.CustomUsers.FirstOrDefault(x => x.PeopleId == pe.Id);
                if (ex == null)
                {
                    activeDirectory.adddOrUpdate(pe, pass);
                    _context.SaveChanges();
                    var account = _context.CustomUsers.FirstOrDefault(x => x.PeopleId == pe.Id);
                    account.AutoGenPass = pass;
                    _context.SaveChanges();
                }
            
            }
            var per = _context.Person.FirstOrDefault(x => x.CUNI == "AEC801205");
            var r = activeDirectory.findUser(per);
            return Ok(r);
        }
        // POST api/People
        [System.Web.Http.HttpPost]
        public IHttpActionResult Post([FromBody]People person)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            //verificar que el ci no exista
            if (_context.Person.FirstOrDefault(p => p.Document == person.Document) != null)
                return BadRequest("El Documento de identidad ya existe.");

            var user = auth.getUser(Request);
            person = validator.CleanName(person);
            
            // verificar si existen personas existentes con una similitud mayor al 90%
            /*var similarities = validator.VerifyExisting(person,0.95f);
            var s = similarities.Count();*/
            //si existe alguna similitud pedir confirmacion
            /*if (s > 0)
            {
                string calconftoken = validator.GetConfirmationToken(similarities);
                IEnumerable<string> confirmationToken = new List<string>();
                dynamic response = new JObject();
                //enviar similitudes en la confirmacion
                response.similarities = JToken.FromObject(similarities); ;
                //generar Token de confirmacion con la lista de similitudes
                response.ConfirmationToken = calconftoken;
                //si ya nos estan confirmado se debe enviar el "ConfirmationToken" en los headers,
                //si este token difiere del token calculado volver a pedir confirmacion
                if (Request.Headers.TryGetValues("ConfirmationToken", out confirmationToken))
                    if (calconftoken != confirmationToken.ElementAt(0))
                        return Ok(response);
                //enviar confirmacion
                else return Ok(response);
            }*/
            
            //si pasa la confirmacion anterior se le asigna un id y se guarda la nueva persona en la BDD
            person.Id = People.GetNextId(_context);
            person.Document = person.Document.ToUpper();
            person.Names = person.Names.ToUpper();
            person.FirstSurName = person.FirstSurName.ToUpper();
            if (!string.IsNullOrEmpty(person.SecondSurName))
            {
                person.SecondSurName = person.SecondSurName.ToUpper();
            }
            if (!string.IsNullOrEmpty(person.MariedSurName))
            {
                person.MariedSurName = person.MariedSurName.ToUpper();
            }
            person = validator.UcbCode(person, user);
            //register updated time
            person.CreatedAt = DateTime.Now;
            _context.Person.Add(person);
            _context.SaveChanges();
            // activeDirectory.addUser(person);

            dynamic res = new JObject();
            res.Id = person.Id;
            res.CUNI = person.CUNI;
            res.Document = person.Document;
            res.FullName = person.GetFullName();

            return Created(new Uri(Request.RequestUri + "/" + person.Id), res);
        }

        [System.Web.Http.NonAction]
        private string cleanText(string text)
        {
            return text.IsNullOrWhiteSpace() ? null : Regex.Replace(text.ToUpper(), @"[\d]", string.Empty).ToUpper();
        }

        // PUT api/People/5
        [System.Web.Http.HttpPut]
        [System.Web.Http.Route("api/people/{id}")]
        public IHttpActionResult Put(int id, [FromBody]People person)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            People personInDB = _context.Person.FirstOrDefault(d => d.Id == id);
            if (personInDB == null)
                return NotFound();

            person = validator.CleanName(person);
            // log changes
            ChangesLogs log = new ChangesLogs();
            log.AddChangesLog(personInDB, person, new List<string>() { "TypeDocument", "Document", "Ext", "Names", "FirstSurName", "SecondSurName", "BirthDate", "Gender", 
                "Nationality", "UseMariedSurName", "UseSecondSurName", "MariedSurName", "PhoneNumber", "PersonalEmail", "OfficePhoneNumber", "OfficePhoneNumberExt",
                "HomeAddress", "UcbEmail", "AFP", "NUA", "Insurance", "InsuranceNumber" });

            //--------------------------REQUIRED COLS--------------------------
            personInDB.TypeDocument = cleanText(person.TypeDocument);
            personInDB.Document = person.Document;
            personInDB.Ext = cleanText(person.Ext);
            personInDB.Names = cleanText(person.Names);
            personInDB.FirstSurName = cleanText(person.FirstSurName);
            personInDB.SecondSurName = cleanText(person.SecondSurName);
            personInDB.BirthDate = person.BirthDate;
            personInDB.Gender = cleanText(person.Gender);
            personInDB.Nationality = cleanText(person.Nationality);
            personInDB.UseMariedSurName = (int) person.UseMariedSurName;
            personInDB.UseSecondSurName = person.UseSecondSurName;
            //------------------------NON REQUIRED COLS--------------------------
            personInDB.MariedSurName = cleanText(person.MariedSurName);
            personInDB.PhoneNumber = person.PhoneNumber;
            personInDB.PersonalEmail = person.PersonalEmail;
            personInDB.OfficePhoneNumber = person.OfficePhoneNumber;
            personInDB.OfficePhoneNumberExt = person.OfficePhoneNumberExt;
            personInDB.HomeAddress = person.HomeAddress;
            personInDB.UcbEmail = person.UcbEmail;
            personInDB.Categoria = person.Categoria;
            personInDB.PhoneNumber = person.PhoneNumber;
            personInDB.AFP = cleanText(person.AFP);
            personInDB.NUA = person.NUA;
            personInDB.Insurance = person.Insurance;
            personInDB.InsuranceNumber = person.InsuranceNumber;
            //register updated time
            personInDB.UpdatedAt = DateTime.Now;




            var ADauth = new ADClass();
            var usr = auth.getUser(Request);
            var rols = ADauth.getUserRols(usr);
            var canUpdatePending = false;
            foreach (var rol in rols)
            {
                if (rol.Name == "GPS Admin" || rol.Name == "Admin")
                {
                    canUpdatePending = true;
                    break;
                }
            }

            if (canUpdatePending)
            {
                personInDB.Pending = person.Pending;
            }
            _context.SaveChanges();
            return Ok(personInDB);
        }

        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/people/FechaIngreso/{id}")]
        public IHttpActionResult FechaIngreso(int id)
        {
            string query = "select p.\"Id\", p.cuni, f.\"FullName\", c.\"StartDate\", lc.\"EndDate\"" +
                           "\r\nfrom " + CustomSchema.Schema +
                           ".\"Antiguedad\" a\r\ninner join " + CustomSchema.Schema + ".\"People\" p on p.\"CUNI\" = a.cuni" +
                           "\r\ninner join " + CustomSchema.Schema + ".\"FullName\" f on f.\"CUNI\" = a.cuni" +
                           "\r\ninner join " + CustomSchema.Schema + ".\"LASTCONTRACTS\" lc on lc.\"CUNI\" = a.cuni" +
                           "\r\ninner join " + CustomSchema.Schema +
                           ".\"ContractDetail\" c on c.\"CUNI\" = a.cuni and" +
                           " c.\"Id\" = a.\"ContractDetailId\"\r\nwhere p.\"Id\" = " + id;
            
            var rawResult = _context.Database.SqlQuery<FechaIngresoAux>(query).Select(x => new
            {
                x.Id,
                x.CUNI,
                StartDateStr = x.StartDate.ToString("dd MMM yyyy", new CultureInfo("es-ES")),
                EndDateStr = x.EndDate == null ? null : x.EndDate.Value.ToString("dd MMM yyyy", new CultureInfo("es-ES")),
            }).AsQueryable();
            return Ok(rawResult);
        }
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/people/AntiguedadAtDate/{id}")]
        public IHttpActionResult AntiguedadAtDate(int id)
        {
            string query = "select " +
                           "\r\n\"StartDate\"," +
                           "\r\nyears_between(\"StartDate\",current_date) \"Años\", " +
                           "\r\nmonths_between(add_years(\"StartDate\", years_between(\"StartDate\",current_date)),current_date) \"Meses\"," +
                           "\r\ndays_between(add_months(to_date(add_years(\"StartDate\", years_between(\"StartDate\",current_date))),months_between(add_years(\"StartDate\", years_between(\"StartDate\",current_date)),current_date)),current_date) \"Dias\"" +
                           "\r\nfrom "+CustomSchema.Schema+".\"ContractDetail\" cd" +
                           "\r\ninner join " + CustomSchema.Schema + ".\"Antiguedad\" a on a.\"ContractDetailId\" = cd.\"Id\" and a.\"CUNI\" = cd.\"CUNI\"" +
                           "\r\nwhere cd.\"PeopleId\" = " + id;

            var rawResult = _context.Database.SqlQuery<AntiguedadAtDate>(query).Select(x => new
            {
                StartDateStr = x.StartDate.ToString("dd MMM yyyy", new CultureInfo("es-ES")),
                x.Años,
                x.Meses,
                x.Dias
            }).AsQueryable();
            return Ok(rawResult);
        }
        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/people/Cargo/{id}")]
        public IHttpActionResult PeopleCargo(int id)
        {
            string query = "select concat (\"PositionDescription\", concat(' en ', \"Dependency\")) as \"PositionDescription\"" +
                           "\r\nfrom \"" + CustomSchema.Schema + "\".\"LASTCONTRACTS_PRIORITY\"\r\nwhere \"PeopleId\" =" + id;

            var rawResult = _context.Database.SqlQuery<ContractDetailListadoSalomon>(query).FirstOrDefault();
            return Ok(rawResult);
        }

    }
}
