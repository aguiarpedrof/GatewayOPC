using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GatewayOPC.Models
{
    [Table("gateway", Schema = "tracker")]
    public class Gateway
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int id { get; set; }

        [Required]
        public string eui { get; set; } = string.Empty;

        [Required]
        public string id_usuario { get; set; } = string.Empty;

        public DateTime? leitura { get; set; }

        public short? status { get; set; }

        public int? numero_trackers { get; set; }

        public int? trackers_tracking_state { get; set; }

        [Required]
        public decimal latitude { get; set; }

        [Required]
        public decimal longitude { get; set; }

        [Required]
        public string ip { get; set; } = string.Empty;

        [Required]
        public short porta { get; set; }

        [Required]
        public string usuario { get; set; } = string.Empty;

        [Required]
        public string senha { get; set; } = string.Empty;

        [Required]
        public float vstow { get; set; }

        [Required]
        public float vdal { get; set; }

        [Required]
        public int restore_time { get; set; }

        [Required]
        public float safe_position_dal_w { get; set; }

        [Required]
        public float safe_position_dal_e { get; set; }

        [Required]
        public float safe_position_stow { get; set; }

        public short modo { get; set; }

        public float target_slope { get; set; }

        public bool automatico { get; set; }

        [Required]
        public float max_slope_west { get; set; }

        [Required]
        public float max_slope_east { get; set; }

        [Required]
        public float cleaning_slope { get; set; }

        [Required]
        public float night_slope { get; set; }

        [Required]
        public double elevation { get; set; }

        [Required]
        public double slope { get; set; }

        [Required]
        public double axis_missalign { get; set; }

        [Required]
        public double azmrotation { get; set; }

        [Required]
        public double atmosrefract { get; set; }

        [Required]
        public double deltaut1 { get; set; }

        [Required]
        public double deltat { get; set; }

        [Required]
        public string applicationid { get; set; } = string.Empty;

        public short id_subcampo { get; set; }

        public bool ativo { get; set; } = true;

        [NotMapped]
        public bool comErro => conectado && status.HasValue && status != 0;

        [NotMapped]
        public bool conectado => ativo && leitura.HasValue && ((TimeSpan)(DateTime.Now - leitura.Value)).TotalMinutes < 10;

        [NotMapped]
        public string descricaoStatus => !ativo ? "Desativado" : (!conectado ? "Desconectado" : (comErro ? "ERRO" : (modo >= 0 && modo <= 4 ? "Tracking" : "Manual")));

        [NotMapped]
        public string descricaoModo =>
            !ativo ? "Desativado" :
            !conectado ? "Desconectado" :
            modo switch
            {
                0 => "Tracking",
                1 => "DAL",
                2 => "STOW",
                3 => "Night",
                4 => "STOP",
                5 => "STOW",
                6 => "Move East",
                7 => "Move West",
                8 => "Target Slope",
                9 => "Cleaning",
                10 => "Emergency",
                _ => ""
            };

        public List<Tracker> Trackers { get; set; } = new();

        public Subcampo? Subcampo { get; set; }
    }
}