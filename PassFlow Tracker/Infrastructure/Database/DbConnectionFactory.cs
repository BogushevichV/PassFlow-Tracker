using Npgsql;
using PassFlow_Tracker.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PassFlow_Tracker.Infrastructure.Database
{
    public class DbConnectionFactory
    {
        public NpgsqlConnection CreateConnection()
        {
            return new NpgsqlConnection(AppConfig.MainConnection);
        }

        public NpgsqlConnection CreateAdminConnection()
        {
            return new NpgsqlConnection(AppConfig.AdminConnection);
        }
    }
}
