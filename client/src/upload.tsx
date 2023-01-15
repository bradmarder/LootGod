import { useState } from 'react';
import './App.css';
import { Row, Col, Alert, Button, Form } from 'react-bootstrap';
import 'bootstrap/dist/css/bootstrap.min.css';
import axios from 'axios';

const api = process.env.REACT_APP_API_PATH;

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
			await axios.post(api + endpoint, formData);
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
			<Form onSubmit={uploadDump}>
				<Row>
					<Col>
						<input type='file' key={key} accept='.txt,.zip' onChange={e => setFile(e.target.files![0])} />
					</Col>
				</Row>
				<br />
				<Button variant='success' disabled={isLoading || file == null} onClick={uploadDump}>Upload</Button>
				<hr />
				{uploaded.map(x =>
					<h6 key={x}>{x}</h6>
				)}
			</Form>
		</Alert>
	);
}