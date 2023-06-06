using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CreateSQLAuditTriggers
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
        SqlConnection connection;
        private void ckAuthType_Checked(object sender, RoutedEventArgs e)
        {
            var cbox = (CheckBox)sender;
            if (cbox == null) return;
            if(cbox.IsChecked== true)
            {
                spUserName.Visibility= Visibility.Collapsed;
            }
            else
            {
                spUserName.Visibility= Visibility.Visible;
            }
        }

        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {

                //connect and populate the databases combo
                string ConString;
                if (ckAuthType.IsChecked.HasValue && ckAuthType.IsChecked.Value)
                    ConString = $"data source={txtServerName.Text};database=master;integrated security=SSPI;Encrypt=False";
                else
                    ConString = $"data source={txtServerName.Text};database=master;User={txtUSerName.Text};Password={txtPassword.Password};Encrypt=False";
                connection = new SqlConnection(ConString);
                SqlDataAdapter da = new SqlDataAdapter("SELECT name FROM sys.databases order by name;", connection);
                DataTable dt = new DataTable();
                da.Fill(dt);
                cbDatabases.Items.Clear();
                foreach (DataRow row in dt.Rows)
                {
                    cbDatabases.Items.Add(row["name"]);
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void cbDatabases_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                //get the tables for selected database
                if (connection.State != ConnectionState.Open)
                    connection.Open();

                connection.ChangeDatabase(cbDatabases.SelectedValue.ToString());
                SqlDataAdapter da = new SqlDataAdapter("SELECT TABLE_NAME FROM [ITDSalesManagement].INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME", connection);
                DataTable dt = new DataTable();
                da.Fill(dt);
                cbTables.Items.Clear();
                foreach (DataRow row in dt.Rows)
                {
                    cbTables.Items.Add(row["TABLE_NAME"]);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void cbTables_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                SqlDataAdapter da = new SqlDataAdapter($"SELECT COLUMN_NAME, ORDINAL_POSITION, DATA_TYPE, IS_NULLABLE, CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION, NUMERIC_SCALE, COLUMN_DEFAULT FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{cbTables.SelectedValue.ToString()}' ORDER BY 2", connection);
                DataTable dt = new DataTable();
                da.Fill(dt);
                lbColumns.Items.Clear();
                foreach (DataRow row in dt.Rows)
                {
                    var tab = new TableColum { COLUMN_NAME = (string)row["COLUMN_NAME"], DATA_TYPE = (string)row["DATA_TYPE"], 
                        IS_NULLABLE= (string)row["IS_NULLABLE"],
                    CHARACTER_MAXIMUM_LENGTH=row["CHARACTER_MAXIMUM_LENGTH"]== System.DBNull.Value ? 0 : (int)row["CHARACTER_MAXIMUM_LENGTH"], 
                        NUMERIC_PRECISION= row["NUMERIC_PRECISION"]== System.DBNull.Value ? byte.MinValue:(byte)row["NUMERIC_PRECISION"],
                        NUMERIC_SCALE= row["NUMERIC_SCALE"]== System.DBNull.Value ? 0:(int)row["NUMERIC_SCALE"],
                        //COLUMN_DEFAULT = row["COLUMN_DEFAULT"]== System.DBNull.Value ? "":  (string)row["COLUMN_DEFAULT"]
                    };
                    lbColumns.Items.Add(tab);
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        

        private void btnCreate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                mainGrid.IsEnabled = false;
                //validate
                //if (ckInsertTrigger.IsChecked != true && ckUpdateTrigger.IsChecked != true && ckDeleteTrigger.IsChecked != true) 
                //{
                //    MessageBox.Show("Please Select at least one trigger type to create");
                //    return;
                //}
                if(lbColumns.SelectedItems.Count <= 0 )
                {
                    MessageBox.Show("Please Select at least one Column to be audited");
                    return;
                }
                var selectedColumns = lbColumns.SelectedItems ;
                
                //create audit table
                string tableColumns = string.Empty;
                string createTableSql = $"IF OBJECT_ID('{cbTables.SelectedValue.ToString()}_Audit', 'U') IS NULL Create TABLE {cbTables.SelectedValue.ToString()}_Audit ([AuditID] [bigint] IDENTITY(1,1) NOT NULL,[AuditType] [nchar](1) NOT NULL,[AuditDate] [datetime2](7) NOT NULL,";
                foreach(TableColum col in selectedColumns)
                {
                    tableColumns = $"{tableColumns}{col.COLUMN_NAME},";
                    createTableSql = $"{createTableSql} [{col.COLUMN_NAME}] [{col.DATA_TYPE}] ";
                    if (col.CHARACTER_MAXIMUM_LENGTH > 0)
                        createTableSql = $"{createTableSql} ({col.CHARACTER_MAXIMUM_LENGTH}) ";
                    if (col.CHARACTER_MAXIMUM_LENGTH ==-1)
                        createTableSql = $"{createTableSql} (max) ";
                    if (col.NUMERIC_PRECISION>0 && col.NUMERIC_SCALE > 0 && col.DATA_TYPE != "money")
                        createTableSql = $"{createTableSql} ({col.NUMERIC_PRECISION},{col.NUMERIC_SCALE}) ";
                    createTableSql = $"{createTableSql} {(col.IS_NULLABLE=="YES"? "NULL,": "NOT NULL,")} ";                   
                }
                createTableSql = $"{createTableSql} CONSTRAINT [PK_{cbTables.SelectedValue.ToString()}_Audit] PRIMARY KEY CLUSTERED ([AuditID] ASC)) ON [PRIMARY]";
                SqlCommand cmd = new SqlCommand(createTableSql, connection);
                cmd.ExecuteNonQuery();
                //create triggers
                string triggerTypes = string.Empty ;
                if (ckInsertTrigger.IsChecked.HasValue && ckInsertTrigger.IsChecked.Value)
                    triggerTypes = "INSERT, UPDATE, DELETE";
                else
                    triggerTypes = "UPDATE, DELETE";
                tableColumns = tableColumns.Remove(tableColumns.Length - 1);//remove the last comma
                string createTrigger = $"CREATE TRIGGER {cbTables.SelectedValue.ToString()}_Triggers ON [{cbTables.SelectedValue.ToString()}] " +
                       $"AFTER {triggerTypes} NOT FOR REPLICATION AS BEGIN SET NOCOUNT ON; " +
                       $"IF EXISTS ( SELECT 0 FROM Deleted ) BEGIN {Environment.NewLine}" +
                       $"IF EXISTS ( SELECT 0 FROM Inserted ) BEGIN  {Environment.NewLine}" +
                       $"INSERT INTO {cbTables.SelectedValue.ToString()}_Audit ([AuditType], [AuditDate],{tableColumns}) SELECT 'U', GETDATE(), {tableColumns} from deleted END " +
                       $"ELSE BEGIN " +
                       $"INSERT INTO {cbTables.SelectedValue.ToString()}_Audit ([AuditType], [AuditDate],{tableColumns}) SELECT 'D', GETDATE(), {tableColumns} from deleted END END " +
                       $"ELSE BEGIN " +
                       $"INSERT INTO {cbTables.SelectedValue.ToString()}_Audit ([AuditType], [AuditDate],{tableColumns}) SELECT 'I', GETDATE(), {tableColumns} from inserted END END";
                cmd.CommandText = createTrigger;
                cmd.ExecuteNonQuery();

                //if (ckInsertTrigger.IsChecked.HasValue && ckInsertTrigger.IsChecked.Value )
                //{
                //    string createTrigger = $"CREATE TRIGGER {cbTables.SelectedValue.ToString()}_InsertTrigger ON {cbTables.SelectedValue.ToString()} " +
                //       $"AFTER UPDATE NOT FOR REPLICATION AS BEGIN SET NOCOUNT ON; " +
                //       $"INSERT INTO {cbTables.SelectedValue.ToString()}_Audit ([AuditType], [AuditDate],{tableColumns}) SELECT 'I', GETDATE(), {tableColumns} from inserted END";
                //    cmd.CommandText= createTrigger ;
                //    cmd.ExecuteNonQuery();
                //}
                //if (ckUpdateTrigger.IsChecked.HasValue && ckUpdateTrigger.IsChecked.Value)
                //{
                //    string createTrigger = $"CREATE TRIGGER {cbTables.SelectedValue.ToString()}_UpdateTrigger ON {cbTables.SelectedValue.ToString()} " +
                //        $"AFTER UPDATE NOT FOR REPLICATION AS BEGIN SET NOCOUNT ON; " +
                //        $"INSERT INTO {cbTables.SelectedValue.ToString()}_Audit ([AuditType], [AuditDate],{tableColumns}) SELECT 'U', GETDATE(), {tableColumns} from deleted END";
                //    cmd.CommandText = createTrigger;
                //    cmd.ExecuteNonQuery();
                //}
                //if (ckDeleteTrigger.IsChecked.HasValue && ckDeleteTrigger.IsChecked.Value)
                //{
                //    string createTrigger = $"CREATE TRIGGER {cbTables.SelectedValue.ToString()}_DeleteTrigger ON {cbTables.SelectedValue.ToString()} " +
                //       $"AFTER UPDATE NOT FOR REPLICATION AS BEGIN SET NOCOUNT ON; " +
                //       $"INSERT INTO {cbTables.SelectedValue.ToString()}_Audit ([AuditType], [AuditDate],{tableColumns}) SELECT 'D', GETDATE(), {tableColumns} from deleted END";
                //    cmd.CommandText = createTrigger;
                //    cmd.ExecuteNonQuery();
                //}
                MessageBox.Show("Operation Completed Successfully");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                mainGrid.IsEnabled= true;
            }
        }
    }

    public class TableColum
    {
        public string COLUMN_NAME { get; set; }
        public string DATA_TYPE { get; set; }
        public string IS_NULLABLE { get; set; }
        public int CHARACTER_MAXIMUM_LENGTH { get; set; }
        public byte NUMERIC_PRECISION { get; set; }
        public int NUMERIC_SCALE { get; set; }
        //public string COLUMN_DEFAULT { get; set; }
    }
}
