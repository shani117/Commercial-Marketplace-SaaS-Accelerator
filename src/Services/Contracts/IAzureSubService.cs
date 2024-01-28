using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Marketplace.SaaS.Accelerator.Services.Contracts;
public interface IAzureSubService
{
    Task<bool> InitializeTenantStorageAndAkv(string tenantName, string tenantId);
}
