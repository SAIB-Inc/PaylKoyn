using System.ComponentModel.DataAnnotations.Schema;
using Chrysalis.Wallet.Models.Keys;

namespace PaylKoyn.Data.Models;

public record Wallet(string Address, int Index, byte[] Key, byte[] ChainCode)
{
    [NotMapped]
    public PrivateKey PrivateKey => new(Key, ChainCode);
};