using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using BeeBot.Models;


namespace YTBot.Services
{
    public class UserService
    {
        private ContextService ContextService { get; set; }

        public UserService()
        {
            ContextService = new ContextService();    
        }

        
    }
}