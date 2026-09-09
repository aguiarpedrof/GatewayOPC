using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GatewayOPC.Models
{
    [Table("subcampo_pontos", Schema = "tracker")]
    public class SubcampoPontos
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public short id_ponto { get; set; }

        [Key]
        public short id_subcampo { get; set; }

        [Required]
        public decimal latitude { get; set; }

        [Required]
        public decimal longitude { get; set; }

        public Subcampo? Subcampo { get; set; }
    }
}
