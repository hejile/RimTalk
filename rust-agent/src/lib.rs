use std::ffi::CStr;
use std::os::raw::c_char;

mod dto;

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
        match serde_json::from_str::<dto::Game>(json_data) {
            Ok(game) => {
                println!("[Rust] Received game info update: {} members, {} maps", 
                    game.player_faction_members.len(), 
                    game.maps.len());
            }
            Err(e) => {
                println!("[Rust] Failed to parse game info: {}", e);
            }
        }
    }
}
