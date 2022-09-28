$(() => {
  const USER_PROFILE_MODAL = '#user-profile-form-modal';

  $(document).on("submit", "form[data-post-update='userProfile']", e => {
    const form = <HTMLFormElement><any>$(e.target);
    
    let serializedForm = form.serializeArray();
    
    $.ajax({
      type: "POST",
      url: $(form).prop("action"),
      data: $.param(serializedForm)
    }).done(res => {
      $(USER_PROFILE_MODAL).modal('hide');
    });
    e.preventDefault();
  });
});