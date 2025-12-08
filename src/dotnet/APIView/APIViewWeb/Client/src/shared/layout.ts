declare const bootstrap: any;

document.addEventListener('DOMContentLoaded', () => {
    // Initialize Bootstrap tooltips and popovers
    document.querySelectorAll('[data-bs-toggle="tooltip"]')
        .forEach(el => new bootstrap.Tooltip(el));
    
    document.querySelectorAll('[data-bs-toggle="popover"]')
        .forEach(el => new bootstrap.Popover(el));

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
                method: 'PUT'
            }).catch(err => console.error('Failed to save theme preference:', err));
        });
    });
});
