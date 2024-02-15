using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Query.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Marketplace.SaaS.Accelerator.DataAccess.Migrations.Custom
{
    internal static class BaselineV741_Seed
    {
        public static void BaselineV741_SeedData(this MigrationBuilder migrationBuilder)
        {
            var seedDate = DateTime.Now;
            migrationBuilder.Sql(@$"
                    IF NOT EXISTS (SELECT * FROM [dbo].[ApplicationConfiguration] WHERE [Name] = 'IsMeteredBillingEnabled')
                    BEGIN
                        INSERT [dbo].[ApplicationConfiguration] ( [Name], [Value], [Description]) VALUES ( N'IsMeteredBillingEnabled', N'true', N'Enable Metered Billing Feature')
                    END
                GO");

            migrationBuilder.Sql(@$"
                    IF NOT EXISTS (SELECT * FROM [dbo].[ApplicationConfiguration] WHERE [Name] = 'WebNotificationHandshake')
                    BEGIN
                        INSERT [dbo].[ApplicationConfiguration] ( [Name], [Value], [Description]) VALUES ( N'WebNotificationHandshake', N'vhd6svGn65t3Os2HQNSh', N'Secret used to handshake between the SaaS portal and the downstream webhooks')
                    END
                GO");
        }

        public static void BaselineV741_DeSeedData(this MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@$"

                IF EXISTS (SELECT * FROM [dbo].[ApplicationConfiguration] WHERE [Name] = 'IsMeteredBillingEnabled')
                BEGIN
                    DELETE FROM [dbo].[ApplicationConfiguration]  WHERE [Name] = 'IsMeteredBillingEnabled'
                END
                GO");

            migrationBuilder.Sql(@$"

                IF EXISTS (SELECT * FROM [dbo].[ApplicationConfiguration] WHERE [Name] = 'WebNotificationHandshake')
                BEGIN
                    DELETE FROM [dbo].[ApplicationConfiguration]  WHERE [Name] = 'WebNotificationHandshake'
                END
                GO");
        }
    }
}