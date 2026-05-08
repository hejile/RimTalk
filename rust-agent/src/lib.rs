use std::ffi::CStr;
use std::os::raw::c_char;

#[unsafe(no_mangle)]
pub extern "C" fn get_rust_magic_number() -> i32 {
    42
}

#[unsafe(no_mangle)]
pub extern "C" fn update_game_info(json_ptr: *const c_char) {
    if json_ptr.is_null() {
        return;
    }
    let c_str = unsafe { CStr::from_ptr(json_ptr) };
    if let Ok(json_data) = c_str.to_str() {
        println!("[Rust] Received game info update ({} bytes)", json_data.len());
    }
}
