use std::{fs::File, panic::catch_unwind, sync::{Mutex, OnceLock}, thread};

use log::{error, info};
use tokio::{runtime::Runtime, sync::oneshot};

use crate::rw_logger::RwLogger;

static LOGGER: RwLogger = RwLogger;
static RUST_THREAD_HANDLE: Mutex<Option<std::thread::JoinHandle<()>>> = Mutex::new(None);
static SHUTDOWN_SIGNAL_SENDER: Mutex<Option<oneshot::Sender<()>>> = Mutex::new(None);

pub fn start() {
    log::set_logger(&LOGGER).unwrap();
    log::set_max_level(log::LevelFilter::Info);
    info!("[RimAgent Rust] Logger initialized.");
    let handle = thread::spawn(move || {
        let r = catch_unwind(move || {
            let (tx, rx) = tokio::sync::oneshot::channel::<()>();
            *SHUTDOWN_SIGNAL_SENDER.lock().unwrap() = Some(tx);
            thread_main(rx);
        });
        match r {
            Ok(_) => info!("[RimAgent Rust] Rust thread exited normally."),
            Err(_) => error!("[RimAgent Rust] Rust thread panicked."),
        };
    });
    if let Ok(mut thread_handle) = RUST_THREAD_HANDLE.lock() {
        *thread_handle = Some(handle);
    }
}

pub fn exit() {
    info!("[RimAgent Rust] rust_exit called.");
    if let Some(tx) = SHUTDOWN_SIGNAL_SENDER.lock().unwrap().take() {
        let _ = tx.send(());
    }
    if let Ok(mut thread_handle) = RUST_THREAD_HANDLE.lock() {
        if let Some(handle) = thread_handle.take() {
            handle.join().unwrap_or_else(|_| {
                error!("[RimAgent Rust] Failed to join Rust thread.");
            });
        }
    }
}

fn thread_main(shutdown_signal_rx: oneshot::Receiver<()>) {
    let cwd = std::env::current_dir();
    info!("[RimAgent Rust] thread started. Current working directory: {:?}", cwd);
    let mut f = match File::create("rust_agent_log.txt") {
        Ok(file) => file,
        Err(e) => {
            error!("[RimAgent Rust] Failed to create log file: {}", e);
            return;
        }
    };
    let _ = std::io::Write::write_all(&mut f, b"RimAgent Rust thread started.\n");

    let rt = tokio::runtime::Builder::new_current_thread()
        .enable_all()
        .build()
        .expect("failed to create tokio runtime");
 
    rt.block_on(async {
        tokio::select! {
            _ = shutdown_signal_rx => {
                println!("tokio shutdown signal received");
            }
            _ = async {
                // todo: main async logic here
                loop {
                    tokio::time::sleep(std::time::Duration::from_secs(100)).await;
                }
            } => {}
        }
    });
}