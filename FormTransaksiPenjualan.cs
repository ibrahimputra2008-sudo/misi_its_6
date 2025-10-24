using ManajemenToko;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Tugas2
{
    public partial class FormTransaksiPenjualan : Form
    {
        public FormTransaksiPenjualan()
        {
            InitializeComponent();
        }


        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void btnSimpan_Click(object sender, EventArgs e)
        {
            if (dgvItem.Rows.Count == 0)
            {
                MessageBox.Show("Belum ada item ditambahkan.");
                return;
            }
            using (SqlConnection conn = Koneksi.GetConnection())
            {
                conn.Open();
                SqlTransaction trx = conn.BeginTransaction();
                try
                {
                    // 1. Insert ke Penjualan
                    decimal total = dgvItem.Rows.Cast<DataGridViewRow>()
                    .Sum(r => Convert.ToDecimal(r.Cells["Subtotal"].Value));
                    SqlCommand cmdPenjualan = new SqlCommand(
                    "INSERT INTO Penjualan (Tanggal, TotalHarga) VALUES (@tgl,@total); SELECT SCOPE_IDENTITY(); ",conn, trx);
                    cmdPenjualan.Parameters.AddWithValue("@tgl", DateTime.Now);
                    cmdPenjualan.Parameters.AddWithValue("@total", total);
                    int penjualanId = Convert.ToInt32(cmdPenjualan.ExecuteScalar());
                    // 2. Insert ke PenjualanDetail
                    foreach (DataGridViewRow row in dgvItem.Rows)
                    {
                        // 1. Simpan detail transaksi
                        SqlCommand cmdDetail = new SqlCommand(
                        @"INSERT INTO PenjualanDetail (PenjualanId, ProdukId, Jumlah, Subtotal)
                        VALUES (@pjId, @prodId, @jumlah, @subtotal)", conn, trx);
                        cmdDetail.Parameters.AddWithValue("@pjId", penjualanId);
                        cmdDetail.Parameters.AddWithValue("@prodId", row.Cells["ProdukId"].Value);
                        cmdDetail.Parameters.AddWithValue("@jumlah", row.Cells["Jumlah"].Value);
                        cmdDetail.Parameters.AddWithValue("@subtotal", row.Cells["Subtotal"].Value);
                        cmdDetail.ExecuteNonQuery();

                        // 2. Kurangi stok produk sesuai jumlah terjual
                        SqlCommand cmdUpdateStok = new SqlCommand(
                        @"UPDATE Produk SET Stok = Stok - @jumlah WHERE Id = @prodId", conn, trx);
                        cmdUpdateStok.Parameters.AddWithValue("@jumlah", row.Cells["Jumlah"].Value);
                        cmdUpdateStok.Parameters.AddWithValue("@prodId", row.Cells["ProdukId"].Value);
                        cmdUpdateStok.ExecuteNonQuery();
                    }

                    trx.Commit();
                    MessageBox.Show("Transaksi berhasil disimpan!");
                    dgvItem.Rows.Clear();
                    HitungTotal();
                }
                catch (Exception ex)
                {
                    trx.Rollback();
                    MessageBox.Show("Gagal menyimpan transaksi: " + ex.Message);
                }
            }
        }

        private void HitungTotal()
        {
            decimal total = 0;
            foreach (DataGridViewRow row in dgvItem.Rows)
            {
                total += Convert.ToDecimal(row.Cells["Subtotal"].Value);
            }
            lblTotal.Text = $"Total: Rp {total:N0}";
        }

        private decimal GetHargaProduk(int produkId)
        {
            using (SqlConnection conn = Koneksi.GetConnection())
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand("SELECT Harga FROM Produk WHERE Id =@id", conn);
                cmd.Parameters.AddWithValue("@id", produkId);
                return (decimal)cmd.ExecuteScalar();
            }
        }

        private void btnTambah_Click(object sender, EventArgs e)
        {
            if (cmbProduk.SelectedItem == null || !int.TryParse(txtJumlah.Text, out
            int jumlah) || jumlah <= 0)
            {
                MessageBox.Show("Pilih produk dan jumlah valid.");
                return;
            }
            var selected = (KeyValuePair<int, string>)cmbProduk.SelectedItem;
            int produkId = selected.Key;
            string namaProduk = selected.Value;
            decimal harga = GetHargaProduk(produkId);
            decimal subtotal = harga * jumlah;
            dgvItem.Rows.Add(produkId, namaProduk, harga, jumlah, subtotal);
            HitungTotal();
        }

        private void FormTransaksiPenjualan_Load(object sender, EventArgs e)
        {
            using (SqlConnection conn = Koneksi.GetConnection())
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand("SELECT Id, NamaProduk FROM Produk",
                conn);
                SqlDataReader reader = cmd.ExecuteReader();
                Dictionary<int, string> produkDict = new Dictionary<int, string>();
                while (reader.Read())
                {
                    produkDict.Add((int)reader["Id"],
                    reader["NamaProduk"].ToString());
                }
                cmbProduk.DataSource = new BindingSource(produkDict, null);
                cmbProduk.DisplayMember = "Value";
                cmbProduk.ValueMember = "Key";
            }
            // Setup kolom dgvItem
            dgvItem.Columns.Add("ProdukId", "ProdukId");
            dgvItem.Columns["ProdukId"].Visible = false;
            dgvItem.Columns.Add("NamaProduk", "Nama Produk");
            dgvItem.Columns.Add("Harga", "Harga");
            dgvItem.Columns.Add("Jumlah", "Jumlah");
            dgvItem.Columns.Add("Subtotal", "Subtotal");

            // Tambahkan kolom tombol Hapus
            DataGridViewButtonColumn btnHapus = new DataGridViewButtonColumn();
            btnHapus.HeaderText = "Aksi";
            btnHapus.Text = "Hapus";
            btnHapus.Name = "Hapus";
            btnHapus.UseColumnTextForButtonValue = true;
            dgvItem.Columns.Add(btnHapus);

            // Hubungkan event klik tombol
            dgvItem.CellContentClick += dgvItem_CellContentClick;
        }

        private void dgvItem_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == dgvItem.Columns["Hapus"].Index)
            {

                DialogResult result = MessageBox.Show(
                    "Yakin ingin menghapus baris ini?",
                    "Konfirmasi Hapus",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning
                );

                if (result == DialogResult.Yes)
                {
                    dgvItem.Rows.RemoveAt(e.RowIndex);

                    HitungTotal();
                }
            }
        }

    }
}
