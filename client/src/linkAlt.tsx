import { useState, useEffect } from 'react';
import { Alert, Button, Form, ListGroup } from 'react-bootstrap';
import axios from 'axios';

export default function LinkAlt(props: { refreshLinkedAltsCacheKey: () => void }) {

	const [isLoading, setIsLoading] = useState(true);
	const [altName, setAltName] = useState('');
    const [result, setResult] = useState<number | undefined>();
    const [linkedAlts, setLinkedAlts] = useState<string[]>([]);

	const linkAlt = () => {
        setResult(undefined);
		setIsLoading(true);
		axios
            .post<number>('/LinkAlt?altName=' + altName)
            .then(x => {
                setResult(x.data);
                setAltName('');
                loadLinkedAlts();
                props.refreshLinkedAltsCacheKey();
            })
            .finally(() => setIsLoading(false));
	};
    const loadLinkedAlts = () => {
        axios
            .get<string[]>('/GetLinkedAlts')
            .then(x => setLinkedAlts(x.data))
            .finally(() => setIsLoading(false));
    };
    useEffect(loadLinkedAlts, []);

	return (
		<Alert variant='dark'>
            <h5>Currently Linked Alts</h5>
            <ListGroup>
                {linkedAlts.map(x => 
                    <ListGroup.Item key={x}>{x}</ListGroup.Item>
                )}
            </ListGroup>
            <hr />
			<Form onSubmit={e => { e.preventDefault(); linkAlt(); }}>
				<Form.Group>
					<Form.Label>Link Alt by Name</Form.Label>
					<Form.Control type="text" placeholder='Enter alt name' value={altName} onChange={e => setAltName(e.target.value)} />
				</Form.Group>
				<br />
				<Button variant='success' disabled={isLoading || altName.length < 4} onClick={linkAlt}>Link</Button>
			</Form>
            {result === 1 &&
                <Alert variant='success'>Successfully linked alt. You earn raid credit on your main for any raid dumps that include your alt.</Alert>
            }
            {result === 0 &&
                <Alert variant='danger'>Failed to link alt. Either you spelled the name wrong, or they aren't in the guild, or someone has already linked this alt, or they are not flagged as an "alt" in the EQ UI guild window.</Alert>
            }
		</Alert>
	);
}