declare const bootstrap: any;

document.addEventListener('DOMContentLoaded', () => {
    // Initialize Bootstrap tooltips and popovers
    document.querySelectorAll('[data-bs-toggle="tooltip"]')
        .forEach(el => new bootstrap.Tooltip(el));
    
    document.querySelectorAll('[data-bs-toggle="popover"]')
        .forEach(el => new bootstrap.Popover(el));

    // Check if user is admin and show admin settings link
    const adminSettingsItem = document.querySelector('.admin-settings-item');
    if (adminSettingsItem) {
        fetch('/api/Permissions/me', { credentials: 'include' })
            .then(response => {
                if (response.ok) {
                    return response.json();
                }
                throw new Error('Failed to fetch permissions');
            })
            .then(permissions => {
                const isAdmin = permissions?.roles?.some(
                    (role: { kind: string; role: string }) => role.kind === 'global' && role.role.toLowerCase() === 'admin'
                );
                if (isAdmin) {
                    adminSettingsItem.classList.remove('d-none');
                    const adminLink = adminSettingsItem.querySelector('a');
                    if (adminLink) {
                        const host = window.location.hostname;
                        let spaBaseUrl: string;
                        if (host === 'localhost' || host === '127.0.0.1') {
                            spaBaseUrl = 'https://localhost:4200';
                        } else {
                            spaBaseUrl = `${window.location.protocol}//spa.${host}`;
                        }
                        adminLink.setAttribute('href', `${spaBaseUrl}/admin/permissions`);
                    }
                }
            })
            .catch(err => console.error('Failed to check admin status:', err));
    }

    // Theme switcher
    const validThemes = ['light-theme', 'dark-theme', 'dark-solarized-theme'];
    
    document.querySelectorAll('.theme-btn').forEach(btn => {
        btn.addEventListener('click', function(this: HTMLElement, e: Event) {
            e.preventDefault();
            e.stopPropagation();
            
            const newTheme = this.getAttribute('data-theme');
            if (!newTheme || !validThemes.includes(newTheme)) return;
            
            document.querySelectorAll('.theme-btn').forEach(b => b.classList.remove('active'));
            this.classList.add('active');
            
            document.body.classList.remove(...validThemes);
            document.body.classList.add(newTheme);
            
            fetch(`/userprofile/updatetheme?theme=${encodeURIComponent(newTheme)}`, {
                method: 'PUT',
                credentials: 'include'
            }).catch(err => console.error('Failed to save theme preference:', err));
        });
    });
});
