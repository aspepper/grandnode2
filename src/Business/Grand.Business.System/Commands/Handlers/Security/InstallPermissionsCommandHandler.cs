﻿using Grand.Business.Core.Commands.System.Security;
using Grand.Business.Core.Interfaces.Common.Directory;
using Grand.Business.Core.Interfaces.Common.Localization;
using Grand.Business.Core.Interfaces.Common.Security;
using Grand.Domain.Customers;
using Grand.Domain.Localization;
using Grand.Domain.Permissions;
using MediatR;

namespace Grand.Business.System.Commands.Handlers.Security;

public class InstallPermissionsCommandHandler : IRequestHandler<InstallPermissionsCommand, bool>
{
    private readonly IGroupService _groupService;
    private readonly ILanguageService _languageService;
    private readonly IPermissionService _permissionService;
    private readonly ITranslationService _translationService;

    public InstallPermissionsCommandHandler(
        IPermissionService permissionService,
        IGroupService groupService,
        ITranslationService translationService,
        ILanguageService languageService)
    {
        _permissionService = permissionService;
        _groupService = groupService;
        _translationService = translationService;
        _languageService = languageService;
    }

    public async Task<bool> Handle(InstallPermissionsCommand request, CancellationToken cancellationToken)
    {
        //install new permissions
        var permissions = request.PermissionProvider.GetPermissions();
        foreach (var permission in permissions)
        {
            var permission1 = await _permissionService.GetPermissionBySystemName(permission.SystemName);
            if (permission1 != null) continue;
            //new permission (install it)
            permission1 = new Permission {
                Name = permission.Name,
                SystemName = permission.SystemName,
                Area = permission.Area,
                Category = permission.Category,
                Actions = permission.Actions
            };


            //default customer group mappings
            var defaultPermissions = request.PermissionProvider.GetDefaultPermissions();
            foreach (var defaultPermission in defaultPermissions)
            {
                var customerGroup =
                    await _groupService.GetCustomerGroupBySystemName(defaultPermission.CustomerGroupSystemName);
                if (customerGroup == null)
                {
                    //new role (save it)
                    customerGroup = new CustomerGroup {
                        Name = defaultPermission.CustomerGroupSystemName,
                        Active = true,
                        SystemName = defaultPermission.CustomerGroupSystemName
                    };
                    await _groupService.InsertCustomerGroup(customerGroup);
                }


                var defaultMappingProvided = (from p in defaultPermission.Permissions
                    where p.SystemName == permission1.SystemName
                    select p).Any();
                if (defaultMappingProvided) permission1.CustomerGroups.Add(customerGroup.Id);
            }

            //save new permission
            await _permissionService.InsertPermission(permission1);

            //save localization
            await SaveTranslationPermissionName(permission1);
        }

        return true;
    }
    private async Task SaveTranslationPermissionName(Permission permissionRecord)
    {
        var name = $"Permission.{permissionRecord.SystemName}";
        var value = permissionRecord.Name;

        foreach (var lang in await _languageService.GetAllLanguages(true))
        {
            var lsr = await _translationService.GetTranslateResourceByName(name, lang.Id);
            if (lsr == null)
            {
                lsr = new TranslationResource {
                    LanguageId = lang.Id,
                    Name = name,
                    Value = value
                };
                await _translationService.InsertTranslateResource(lsr);
            }
            else
            {
                lsr.Value = value;
                await _translationService.UpdateTranslateResource(lsr);
            }
        }
    }
}