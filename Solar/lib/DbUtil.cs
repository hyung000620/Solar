using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace Solar
{
    public class DbUtil
    {
        string dbConStr = Properties.Settings.Default.dbConStr;
        //string dbConStr = "SERVER=localhost;port=3306;DATABASE=db_main;UID=root;PASSWORD=;convert zero datetime=True";
        public MySqlConnection dbcon;

        public DbUtil()
        {
            dbcon = new MySqlConnection(dbConStr);
        }

        public void Open()
        {            
            dbcon.Open();
        }

        public void Close()
        {
            dbcon.Close();
        }

        public void ExeQry(string sql, List<MySqlParameter> sp = null)
        {
            //Open();
            MySqlCommand cmd = new MySqlCommand(sql, dbcon);
            if (sp != null)
            {
                cmd.Parameters.AddRange(sp.ToArray());
            }
            cmd.ExecuteNonQuery();
            cmd.Dispose();
            //Close();
        }

        public MySqlDataReader ExeRdr(string sql, List<MySqlParameter> sp = null)
        {            
            //Open();
            MySqlCommand cmd = new MySqlCommand(sql, dbcon);
            if (sp != null)
            {
                cmd.Parameters.AddRange(sp.ToArray());
            }
            MySqlDataReader dr = cmd.ExecuteReader();
            //dr.Close();
            //cmd.Dispose();
            //Close();
            
            return dr;
        }

        public MySqlDataAdapter MyAdap(string sql, List<MySqlParameter> sp = null)
        {
            Open();
            MySqlDataAdapter mda = new MySqlDataAdapter(sql, dbcon);
            Close();

            return mda;
        }

        public DataSet ExeDs(string sql, List<MySqlParameter> sp = null)
        {
            Open();
            MySqlCommand cmd = new MySqlCommand(sql, dbcon);
            if (sp != null)
            {
                cmd.Parameters.AddRange(sp.ToArray());
            }
            MySqlDataAdapter mda = new MySqlDataAdapter();
            mda.SelectCommand = cmd;
            DataSet ds = new DataSet();
            mda.Fill(ds);
            mda.Dispose();
            cmd.Dispose();
            Close();

            return ds;
        }

        public DataTable ExeDt(string sql, List<MySqlParameter> sp = null)
        {
            Open();
            MySqlCommand cmd = new MySqlCommand(sql, dbcon);
            if (sp != null)
            {
                cmd.Parameters.AddRange(sp.ToArray());
            }
            MySqlDataAdapter mda = new MySqlDataAdapter();
            mda.SelectCommand = cmd;
            DataTable dt = new DataTable();
            mda.Fill(dt);
            mda.Dispose();
            cmd.Dispose();
            Close();

            return dt;
        }

        public bool ExistRow(string sql, List<MySqlParameter> sp = null)
        {
            bool exist;
            //Open();
            MySqlCommand cmd = new MySqlCommand(sql, dbcon);
            if (sp != null)
            {
                cmd.Parameters.AddRange(sp.ToArray());
            }
            MySqlDataReader dr = cmd.ExecuteReader();
            exist = dr.HasRows;
            dr.Close();
            cmd.Dispose();
            //Close();

            return exist;
        }

        public object RowCnt(string sql, List<MySqlParameter> sp = null)
        {            
            MySqlCommand cmd = new MySqlCommand(sql, dbcon);
            if (sp != null)
            {
                cmd.Parameters.AddRange(sp.ToArray());
            }

            object rowCnt = cmd.ExecuteScalar();
            cmd.Dispose();

            return rowCnt;
        }

        public object LastId()
        {
            MySqlCommand cmd = new MySqlCommand("SELECT LAST_INSERT_ID()", dbcon);
            //MySqlDataReader dr = cmd.ExecuteReader();
            //dr.Read();
            // UInt64 lastId = (UInt64)dr[0];
            //dr.Close();
            object lastId = cmd.ExecuteScalar();
            cmd.Dispose();
            
            return lastId;
        }
    }
}
