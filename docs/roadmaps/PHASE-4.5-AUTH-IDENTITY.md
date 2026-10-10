---
name: auth-identity-ui-templates
description: Auth/Identity UI templates leveraging Modulus.Identity features
metadata:
  node_type: memory
  type: project
  created: 2026-10-10
  originSessionId: 88b0729e-9258-455d-b28b-b0d8f1f35b1d
  modified: 2026-10-10T11:45:40.302Z
---

# Auth/Identity UI Enhancement Layer

## Integration Points with Modulus.Identity

Your framework provides:
- ✅ User management (Modulus.Users)
- ✅ Role-based access control (Modulus.Identity.Authorization)
- ✅ Multi-tenancy support
- ✅ Claims-based authorization
- ✅ Audit logging
- ✅ Permission management

**Opportunity**: Build **15+ professional auth UI templates** that showcase these capabilities.

---

## Phase 4.5: Authentication & Authorization UI

### Template Set A: Login & Registration (8 templates)

**Modern Login Flows** (all 4 engines)
```
- auth/login-basic.{cshtml,razor,liquid}.sbn
- auth/login-social.{cshtml,razor,liquid}.sbn (OAuth: Google, Microsoft, GitHub)
- auth/login-2fa.{cshtml,razor,liquid}.sbn (Two-factor auth)
```

**Registration & Onboarding** (all 4 engines)
```
- auth/register-form.{cshtml,razor,liquid}.sbn
- auth/register-wizard.{cshtml,razor,liquid}.sbn (Multi-step)
- auth/email-verification.{cshtml,razor,liquid}.sbn
- auth/password-reset.{cshtml,razor,liquid}.sbn
```

**Implementation Details**:
```csharp
// Templates inject user claims into views
@inject IAuthorizationService AuthService
@inject CurrentUserContext CurrentUser

// Current user always available
<span>Welcome, @CurrentUser.DisplayName</span>

// Role-based content
@if (User.IsInRole("Admin"))
{
    <button>Admin Tools</button>
}

// Permission-based rendering
@if (await AuthService.AuthorizeAsync(User, "DeleteUsers"))
{
    <button>Delete User</button>
}
```

---

### Template Set B: User Management Dashboard (6 templates)

**User Admin Pages** (MVC + Razor Pages)
```
- users/users-list.{cshtml}.sbn
  - Search, filter by role, sort by last login
  - Bulk actions (activate, deactivate, assign roles)
  - Audit trail for each user

- users/user-detail.{cshtml}.sbn
  - Profile edit
  - Claims management
  - Role assignment
  - Permission matrix
  - Activity history

- users/user-permissions.{cshtml}.sbn
  - Permission builder (tree view)
  - Role-based vs direct permissions
  - Permission conflicts warning
  - Inheritance visualization
```

**Blazor Components**
```
- users/user-manager.razor.sbn
  - Real-time user list (SignalR)
  - Inline editing with auto-save
  - Permission selector component
  - Activity graph

- users/permission-matrix.razor.sbn
  - Row: Users, Column: Permissions
  - Click to grant/revoke
  - Bulk updates
  - Diff view (what changed)
```

---

### Template Set C: Role & Permission Management (5 templates)

**Role Management**
```
- roles/roles-list.{cshtml,razor}.sbn
- roles/role-editor.{cshtml,razor}.sbn
  - Permission tree selector
  - Permission combinations (AND/OR)
  - Sample users with this role
```

**Permission Management**
```
- permissions/permissions-matrix.{cshtml,razor}.sbn
- permissions/permission-builder.{cshtml,razor}.sbn
  - Claims-based rule editor
  - "Create resource if owner" patterns
  - Preview affected users
```

---

### Template Set D: Security & Compliance (7 templates)

**Account Security**
```
- security/change-password.{cshtml,razor}.sbn
  - Password strength indicator
  - History (can't reuse)
  - Force change on expiry

- security/2fa-setup.{cshtml,razor}.sbn
  - QR code for authenticator apps
  - Backup codes
  - Recovery options

- security/active-sessions.{cshtml,razor}.sbn
  - List all active sessions
  - Browser/device info
  - Revoke individual sessions
  - Suspicious activity alerts
```

**Audit & Compliance**
```
- audit/user-activity-log.{cshtml,razor}.sbn
  - What user did, when, from where
  - IP address, browser, device
  - Filter by action type
  - Export to CSV/Excel

- audit/access-review.{cshtml,razor}.sbn
  - "Users with this permission"
  - Permission trend (added/removed over time)
  - Compliance report generator
  - Segregation of duties checker

- audit/security-events.{cshtml,razor}.sbn
  - Failed login attempts (with rate limiting)
  - Permission changes
  - Account lockouts
  - Admin actions on users
  - Anomaly detection (impossible travel, etc.)
```

---

### Template Set E: Multi-Tenancy UI (4 templates)

**Tenant Management** (if multi-tenant)
```
- tenants/tenant-list.{cshtml,razor}.sbn
  - Active tenants
  - User count per tenant
  - Subscription status
  - Data usage

- tenants/tenant-settings.{cshtml,razor}.sbn
  - Custom branding
  - Feature toggles
  - User import/export
  - SSO configuration

- tenants/tenant-isolation-view.{cshtml,razor}.sbn
  - Data isolation verification
  - Cross-tenant permission audit
  - User assignment by tenant

- tenants/tenant-analytics.{cshtml,razor}.sbn
  - Growth over time
  - Feature adoption
  - User engagement by tenant
```

---

## New CLI Commands for Auth/Identity

```bash
# Auth scaffolding
modulus ui add-auth --flow {login|registration|both}
  # Scaffolds: login/register pages with password validation
  # Includes: email confirmation, password reset flows

modulus ui add-2fa --type {totp|sms}
  # Scaffolds: 2FA setup wizard
  # Includes: QR code generator, backup codes, verify flow

# User management
modulus ui create-user-dashboard
  # Scaffolds: complete user admin interface
  # Includes: search, filters, bulk operations, roles

modulus ui create-permission-matrix --entity <entity>
  # Auto-generates: permission matrix based on resource operations
  # Maps to: Create, Read, Update, Delete per role

# Role management
modulus ui add-role-builder
  # Scaffolds: role editor with permission tree
  # Includes: permission search, bulk assign

# Audit & compliance
modulus ui add-audit-viewer
  # Scaffolds: audit log UI with filters
  # Includes: CSV export, anomaly detection

modulus ui add-access-review
  # Scaffolds: access review form/workflow
  # Includes: reviewer assignments, approval flow

# Security
modulus ui add-session-manager
  # Scaffolds: active sessions UI
  # Includes: device detection, session revocation

modulus ui add-security-settings
  # Scaffolds: account security dashboard
  # Includes: password management, 2FA, sessions, activity
```

---

## Integration Patterns

### Pattern 1: Role-Based Rendering

```csharp
// mvc/admin-dashboard.cshtml.sbn
@if (User.IsInRole("Admin"))
{
    <div class="admin-card">
        <h3>System Users</h3>
        <p>Total: @TotalUsers</p>
        <!-- Link to user management -->
        <a href="/Admin/Users">Manage Users</a>
    </div>
}

@if (User.IsInRole("SecurityAdmin"))
{
    <div class="security-card">
        <h3>Security Events</h3>
        <p>Failed Logins: @FailedLoginCount</p>
        <!-- Link to security audit -->
        <a href="/Admin/SecurityEvents">View Events</a>
    </div>
}
```

### Pattern 2: Permission-Based Actions

```csharp
// users/user-list.cshtml.sbn
@foreach (var user in Users)
{
    <tr>
        <td>@user.DisplayName</td>
        <td>
            @if (await AuthService.AuthorizeAsync(User, "EditUser"))
            {
                <a href="/Users/@user.Id/Edit">Edit</a>
            }
        </td>
        <td>
            @if (await AuthService.AuthorizeAsync(User, "DeleteUser"))
            {
                <button @onclick="() => DeleteUser(user.Id)">Delete</button>
            }
        </td>
    </tr>
}
```

### Pattern 3: Claims-Based Customization

```csharp
// Blazor: permission-matrix.razor.sbn
@code {
    private async Task LoadUserPermissions()
    {
        var userClaims = User.Claims;
        var userPermissions = userClaims
            .Where(c => c.Type == "permission")
            .Select(c => c.Value)
            .ToList();
        
        // Render permission matrix based on claims
    }
}
```

### Pattern 4: Audit Trail Integration

```csharp
// Every action is logged
public async Task EditUser(User user)
{
    var original = await _users.GetAsync(user.Id);
    var changeLog = ComputeChanges(original, user);
    
    await _users.UpdateAsync(user);
    
    // Audit logged automatically by middleware
    // Template shows in user-activity-log.cshtml.sbn
}
```

---

## Business Value

### For IT/Security Teams
- ✅ Complete user lifecycle management UI
- ✅ Permission matrix visibility
- ✅ Audit trail for compliance
- ✅ Security event monitoring
- ✅ Multi-tenancy isolation verification

### For Enterprises
- ✅ Meet SOC 2, ISO 27001 requirements (audit logs, access control)
- ✅ RBAC implementation showcase
- ✅ User provisioning/deprovisioning workflows
- ✅ Compliance reporting built-in

### For Developers
- ✅ Copy-paste identity UI implementations
- ✅ Best practices baked in
- ✅ Security patterns enforced
- ✅ User management solved in hours, not weeks

---

## Template Count & CLI Commands

**New Templates**: 30+ (login, 2FA, user management, roles, audit, security, compliance)
- Login & Registration: 8
- User Management: 6
- Role & Permission Management: 5
- Security & Compliance: 7
- Multi-Tenancy: 4

**New CLI Commands**: 12+
```
modulus ui add-auth
modulus ui add-2fa
modulus ui create-user-dashboard
modulus ui create-permission-matrix
modulus ui add-role-builder
modulus ui add-audit-viewer
modulus ui add-access-review
modulus ui add-session-manager
modulus ui add-security-settings
modulus ui add-tenant-management
```

---

## Phasing Strategy

**Option A: Add as Phase 4.5** (between Phase 4 and Phase 5)
- Weeks 1-3 (implement 30+ auth templates)
- Complete before CRUD generation
- Users can scaffold apps with full auth UI from day 1

**Option B: Weave Throughout**
- Phase 5: Add auth examples in CRUD templates
- Phase 6: Admin dashboard includes user management
- Phase 7: Audit logs tie into security audit
- Phase 8+: Permission matrix for advanced features

**Recommendation**: **Option A** — Auth should come early, it's foundational

---

## One-Liner CLI Examples

```bash
# Create complete SaaS app in 4 commands
modulus app SaasApp --ui-engine mvc
modulus ui add-auth --flow both
modulus ui create-user-dashboard
modulus ui create-permission-matrix

# Generates:
# - Login/Register pages
# - User admin dashboard
# - Role management UI
# - Permission matrix
# - Audit logs viewer
# All wired up, ready to customize
```

---

## Success Criteria

- [ ] 30+ auth/identity templates created
- [ ] 12+ CLI commands for auth scaffolding
- [ ] All templates integrate with Modulus.Identity APIs
- [ ] User management workflows complete (CRUD + roles + permissions)
- [ ] Audit trail visible in UI automatically
- [ ] One command creates entire auth UI
- [ ] Enterprise customers adopt for user onboarding
