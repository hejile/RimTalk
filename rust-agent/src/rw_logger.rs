use std::sync::RwLock;

use log::{Level, Metadata, Record};
use serde::{Serialize, ser::SerializeStruct};

static RW_LOG_ENTRIES: RwLock<Vec<LogEntry>> = RwLock::new(Vec::new());

pub struct LogEntry {
    level: Level,
    message: String,
}

impl Serialize for LogEntry {
    fn serialize<S>(&self, serializer: S) -> Result<S::Ok, S::Error>
    where
        S: serde::Serializer,
    {
        let mut state = serializer.serialize_struct("LogEntry", 2)?;
        state.serialize_field("Level", &self.level.to_string())?;
        state.serialize_field("Message", &self.message)?;
        state.end()
    }
}

pub struct RwLogger;

impl log::Log for RwLogger {
    fn enabled(&self, _metadata: &Metadata) -> bool {
        true
    }

    fn log(&self, record: &Record) {
        if self.enabled(record.metadata()) {
            let entry = LogEntry {
                level: record.level(),
                message: format!("{}", record.args()),
            };
            RW_LOG_ENTRIES.write().unwrap().push(entry);
        }
    }

    fn flush(&self) {}
}

pub fn drain_logs() -> Vec<LogEntry> {
    let mut entries = RW_LOG_ENTRIES.write().unwrap();
    let mut logs = Vec::new();
    std::mem::swap(&mut *entries, &mut logs);
    logs
}