using Buriza.Data.Models.Common;
using Buriza.UI.Resources;

namespace Buriza.UI.Data.Dummy;

public static class WalletData
{
    public static List<Wallet> GetDummyWallets()
    {
        return
        [
            new Wallet
            {
                Id = 1,
                Name = "My Savings",
                Avatar = Profiles.Profile1,
                Accounts =
                [
                    new WalletAccount { Name = "Iris Chu", Address = "addr1q9g5p5kz0t9v8f7s6l3d4h8j2k0y5m5c4a7w5p2x9u8z7y5n2qz8f6dfsph9", DerivationPath = "m/1852'/1815'/1'", Avatar = Profiles.Profile1, IsActive = true },
                    new WalletAccount { Name = "Account 2", Address = "addr1q8h4n3kz1t8v7f6s5l2d3h7j1k9y4m4c3a6w4p1x8u7z6y4n1qz7f5j2h9b0", DerivationPath = "m/1852'/1815'/2'", Avatar = Profiles.Profile2, IsActive = false },
                    new WalletAccount { Name = "Account 3", Address = "addr1q7g3m2kz2t7v6f5s4l1d2h6j0k8y3m3c2a5w3p0x7u6z5y3n0qz6f4j1h8c1", DerivationPath = "m/1852'/1815'/3'", Avatar = Profiles.Profile3, IsActive = false },
                    new WalletAccount { Name = "Account 4", Address = "addr1q6f2k1kz3t6v5f4s3l0d1h5j9k7y2m2c1a4w2p9x6u5z4y2n9qz5f3k7h2d4", DerivationPath = "m/1852'/1815'/4'", Avatar = Profiles.Profile4, IsActive = false },
                    new WalletAccount { Name = "Account 5", Address = "addr1q5e1j0kz4t5v4f3s2l9d0h4j8k6y1m1c0a3w1p8x5u4z3y1n8qz4f2l8h1e5", DerivationPath = "m/1852'/1815'/5'", Avatar = Profiles.Profile5, IsActive = false },
                    new WalletAccount { Name = "Account 6", Address = "addr1q4d0i9kz5t4v3f2s1l8d9h3j7k5y0m0c9a2w0p7x4u3z2y0n7qz3f1m9h0f6", DerivationPath = "m/1852'/1815'/6'", Avatar = Profiles.Profile6, IsActive = false },
                    new WalletAccount { Name = "Account 7", Address = "addr1q3c9h8kz6t3v2f1s0l7d8h2j6k4y9m9c8a1w9p6x3u2z1y9n6qz2f0n0h9g7", DerivationPath = "m/1852'/1815'/7'", Avatar = Profiles.Profile7, IsActive = false }
                ]
            },
            new Wallet
            {
                Id = 2,
                Name = "Cash Cache",
                Avatar = Profiles.Profile4,
                Accounts =
                [
                    new WalletAccount { Name = "Account 1", Address = "addr1q6f2l1kz3t6v5f4s3l0d1h5j9k7y2m2c1a4w2p9x6u5z4y2n9qz5f3j0h7d2", DerivationPath = "m/1852'/1815'/1'", Avatar = Profiles.Profile4, IsActive = false },
                    new WalletAccount { Name = "Account 2", Address = "addr1q5e1k0kz4t5v4f3s2l9d0h4j8k6y1m1c0a3w1p8x5u4z3y1n8qz4f2j9h6e3", DerivationPath = "m/1852'/1815'/2'", Avatar = Profiles.Profile5, IsActive = false }
                ]
            },
            new Wallet
            {
                Id = 3,
                Name = "Future Fund",
                Avatar = Profiles.Profile5,
                Accounts =
                [
                    new WalletAccount { Name = "Account 1", Address = "addr1q4d0j9kz5t4v3f2s1l8d9h3j7k5y0m0c9a2w0p7x4u3z2y0n7qz3f1j8h5f4", DerivationPath = "m/1852'/1815'/1'", Avatar = Profiles.Profile6, IsActive = false }
                ]
            },
            new Wallet
            {
                Id = 4,
                Name = "Crypto Chest",
                Avatar = Profiles.Profile6,
                Accounts =
                [
                    new WalletAccount { Name = "Account 1", Address = "addr1q3c9i8kz6t3v2f1s0l7d8h2j6k4y9m9c8a1w9p6x3u2z1y9n6qz2f0j7h4g5", DerivationPath = "m/1852'/1815'/1'", Avatar = Profiles.Profile7, IsActive = false },
                    new WalletAccount { Name = "Account 2", Address = "addr1q2b8h7kz7t2v1f0s9l6d7h1j5k3y8m8c7a0w8p5x2u1z0y8n5qz1f9j6h3h6", DerivationPath = "m/1852'/1815'/2'", Avatar = Profiles.Profile8, IsActive = false }
                ]
            },
            new Wallet
            {
                Id = 5,
                Name = "Digital Safe",
                Avatar = Profiles.Profile7,
                Accounts =
                [
                    new WalletAccount { Name = "Account 1", Address = "addr1q1a7g6kz8t1v0f9s8l5d6h0j4k2y7m7c6a9w7p4x1u0z9y7n4qz0f8j5h2i7", DerivationPath = "m/1852'/1815'/1'", Avatar = Profiles.Profile8, IsActive = false }
                ]
            }
        ];
    }
}
