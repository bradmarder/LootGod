import { useState } from 'react';
import { Alert, Form } from 'react-bootstrap';
import axios from 'axios';

export default function Upload(props: { refreshCache: () => void }) {

	const [isLoading, setIsLoading] = useState(false);
	const [key, setKey] = useState(1);
	const [uploaded, setUploaded] = useState<string[]>([]);

	const uploadDump = (file: File) => {
		setIsLoading(true);
		const formData = new FormData();
		formData.append("file", file);
		axios
			.post('/ImportDump?offset=' + new Date().getTimezoneOffset(), formData)
			.then(() => {
				setUploaded([...uploaded, key + ' - ' + file.name]);
				props.refreshCache();
			})
			.finally(() => {
				setKey(key + 1);
				setIsLoading(false);
			});
	};

	return (
		<Alert variant='primary'>
			<h4>Upload Guild/Raid Dumps (Admin only)</h4>
			<hr />
			<Form>
				<input type='file' key={key} accept='.txt,.zip' disabled={isLoading} onChange={e => uploadDump(e.target.files![0]!)} />
				<hr />
				{uploaded.length > 0 &&
					<Alert variant='success'>
						{uploaded.map(x =>
							<h6 key={x}>{x}</h6>
						)}
					</Alert>
				}
				<a target="_blank" rel="noreferrer" href={'/api/GetPasswords?playerKey=' + localStorage.getItem('key')}>Download Master Password Links (Leader Only)</a>
			</Form>
		</Alert>
	);
}