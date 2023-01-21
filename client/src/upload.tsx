import { useState } from 'react';
import './App.css';
import { Alert, Button, Form } from 'react-bootstrap';
import 'bootstrap/dist/css/bootstrap.min.css';
import axios from 'axios';

export function Upload() {

	const [isLoading, setIsLoading] = useState(false);
	const [key, setKey] = useState(0);
	const [file, setFile] = useState<File | null>(null);
	const [uploaded, setUploaded] = useState<string[]>([]);

	const uploadDump = async () => {
		setIsLoading(true);
		try {
			const endpoint = file!.name.endsWith('.zip') ? '/BulkImportRaidDump'
				 : file!.name.startsWith('RaidRoster') ? '/ImportRaidDump'
				 : '/ImportGuildDump';
			const formData = new FormData();
			formData.append("file", file!);
			await axios.post(endpoint + '?offset=' + new Date().getTimezoneOffset(), formData);
			setUploaded([...uploaded, file!.name]);
		}
		finally {
			setFile(null);
			setKey(key + 1);
			setIsLoading(false);
		}
	};

	return (
		<Alert variant='primary'>
			<h4>Upload Guild/Raid Dumps (Admin only)</h4>
			<hr />
			<Form onSubmit={uploadDump}>
				<input type='file' key={key} accept='.txt,.zip' onChange={e => setFile(e.target.files![0])} />
				<hr />
				<Button variant='success' disabled={isLoading || file == null} onClick={uploadDump}>Upload</Button>
				<hr />
				{uploaded.map(x =>
					<h6 key={x}>{x}</h6>
				)}
			</Form>
		</Alert>
	);
}