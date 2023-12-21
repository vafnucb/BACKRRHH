using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web;
using UcbBack.Models;
using UcbBack.Models.Auth;
using System.Data.Entity;
using System.Diagnostics;
using System.DirectoryServices.ActiveDirectory;
using Microsoft.CSharp.RuntimeBinder;

namespace UcbBack.Logic
{
    public class ValidateAuth
    {
        private ApplicationDbContext _context;
        private ADClass activeDirectory;
        public int tokenLife = 10*60;
        public int refeshtokenLife = 4*60*60;


        public ValidateAuth()
        {
            _context = new ApplicationDbContext();
            activeDirectory = new ADClass();
        }

        public bool isAuthenticated(int id, string token)
        {
            CustomUser user = _context.CustomUsers.FirstOrDefault(u=> u.Id ==id);
            
            if (user == null || user.Token != token)
            {
                return false;
            }
            var now = DateTime.Now;
            if (user.TokenCreatedAt == null)
                return false;
            int seconds = (int)now.Subtract(user.TokenCreatedAt.Value).TotalSeconds;
            if (seconds > tokenLife)
            {
                user.Token = null;
                user.TokenCreatedAt = null;
                _context.SaveChanges();
                return false;
            }
            
            return true;
        }

        public bool hasAccess(int id, string path,string method)
        {
            CustomUser user = _context.CustomUsers.Include(x=>x.People).FirstOrDefault(u => u.Id == id);

            if (user == null)
            {
                return false;
            }

            if (activeDirectory.memberOf(user, "Personas.Admin"))
            {
                return true;
            }

            Access access = _context.Accesses.FirstOrDefault(a => a.Path == path && a.Method == method);

            if (access == null)
            {
                return false;
            }

            RolhasAccess rolhasAccess =
                _context.RolshaAccesses.FirstOrDefault(ra => ra.Accessid == access.Id );
            if (rolhasAccess == null)
            {
                return false;
            }

            return true;
        }

        public bool isPublic(string path, string method)
        {
            Access access = _context.Accesses.FirstOrDefault(a =>
                string.Equals(a.Path.ToUpper(), path.ToUpper()) && a.Method == method);

            return access==null?false:access.Public;
        }

        public bool nedAuth(string path, string method)
        {
            Access access = _context.Accesses.FirstOrDefault(a =>
                string.Equals(a.Path.ToUpper(), path.ToUpper()) && a.Method == method);

            return access == null ? false : access.NedAuth;
        }

        public bool shallYouPass(int id, string token, string path, string method,out AccessLogs log)
        {
            
            bool pass = true;

            path = path.EndsWith("/")? path.Substring(0, path.Length - 1):path;
            path = path.StartsWith("/")? path.Substring(1, path.Length-1):path;

            bool ispublic = isPublic(path, method);
            bool isauthenticated = isAuthenticated(id, token);
            bool nedauth = nedAuth(path, method);
            bool hasaccess = hasAccess(id,path,method);

            if (nedauth && !isauthenticated) pass = false;
            if (!ispublic && !hasaccess && !isauthenticated) pass = false;

            Access access = _context.Accesses.FirstOrDefault(a =>
                string.Equals(a.Path.ToUpper(), path.ToUpper()) && a.Method == method);

            if (access == null || access.Id != 19)
            {
                log = new AccessLogs();
                log.Id = AccessLogs.GetNextId(_context);
                log.Method = method;
                log.Path = path;
                log.UserId = (id == 0 ? null : (int?)id);
                log.Success = pass;
                log.AccessId = access == null ? null : (int?)access.Id;
                _context.AccessLogses.Add(log);
                _context.SaveChanges();
            }
            else
                log = null;

            return pass; 
        }

        public CustomUser getUser(HttpRequestMessage request)
        {
            IEnumerable<string> idlist;
            int userid;
            if (!request.Headers.TryGetValues("id", out idlist))
            {
                return null;
            }

            if (!Int32.TryParse(idlist.First(), out userid))
            {
                return null;
            }

            var user = _context.CustomUsers.Include(x=>x.People).FirstOrDefault(u => u.Id == userid);

            return user;
        }

        public IQueryable<dynamic> filerByRegional(IQueryable<dynamic> list, CustomUser user,bool isBranchtable=false, bool onlyActive = true)
        {
            IQueryable<dynamic> res=list;
            if (!activeDirectory.memberOf(user, "Personas.Admin"))
            {
                var brs = activeDirectory.getUserBranches(user);
                var br = brs.Select(x => x.Id).ToList();
                
                if (isBranchtable)
                {
                    res = list.ToList().Where(x => br.Contains(x.Id)).AsQueryable();

                }
                else
                {
                    res = list.ToList().Where(x => br.Contains(x.BranchesId)).AsQueryable();
                    try
                    {
                        //try to filter bt active if table has active property
                        if (onlyActive)
                            res = res.ToList().Where(x => x.Active == true).ToList().AsQueryable();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    } 
                }
            }           
            return res;
        }

    }
}